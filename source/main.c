#include "switch/kernel/event.h"
#include "switch/kernel/svc.h"
#include "switch/kernel/thread.h"
#include "switch/result.h"
#include "switch/runtime/pad.h"
#include "switch/services/apm.h"
#include "switch/services/applet.h"
#include "switch/services/hid.h"
#include "switch/services/vi.h"
#include "switch/types.h"
#include <limits.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <arpa/inet.h>
#include <switch.h>
#include <sys/socket.h>
#include <unistd.h>

#define INNER_HEAP_SIZE 0x80000

u32 __nx_applet_type = AppletType_None;

u32 __nx_fs_num_sessions = 1;

void __libnx_initheap(void) {
  static u8 inner_heap[INNER_HEAP_SIZE];
  extern void *fake_heap_start;
  extern void *fake_heap_end;

  fake_heap_start = inner_heap;
  fake_heap_end = inner_heap + sizeof(inner_heap);
}

void __appInit(void) {
  Result rc;

  rc = smInitialize();
  if (R_FAILED(rc))
    diagAbortWithResult(MAKERESULT(Module_Libnx, LibnxError_InitFail_SM));

  rc = setsysInitialize();
  if (R_SUCCEEDED(rc)) {
    SetSysFirmwareVersion fw;
    rc = setsysGetFirmwareVersion(&fw);
    if (R_SUCCEEDED(rc))
      hosversionSet(MAKEHOSVERSION(fw.major, fw.minor, fw.micro));
    setsysExit();
  }

  rc = hidInitialize();
  if (R_FAILED(rc))
    diagAbortWithResult(MAKERESULT(Module_Libnx, LibnxError_InitFail_HID));

  static const SocketInitConfig socketInitConfig = {
      .bsdsockets_version = 1,

      .tcp_tx_buf_size = 1024,
      .tcp_rx_buf_size = 256,
      .tcp_tx_buf_max_size = 0,
      .tcp_rx_buf_max_size = 0,

      .udp_tx_buf_size = 0x2400,
      .udp_rx_buf_size = 0xA500,

      .sb_efficiency = 2,

      .num_bsd_sessions = 3,
      .bsd_service_type = BsdServiceType_User,
  };

  rc = socketInitialize(&socketInitConfig);
  if (R_FAILED(rc))
    diagAbortWithResult(MAKERESULT(Module_Libnx, LibnxError_ShouldNotHappen));

  rc = viInitialize(ViServiceType_System);
  if (R_FAILED(rc))
    diagAbortWithResult(MAKERESULT(Module_Libnx, LibnxError_ShouldNotHappen));

  // rc = appletInitialize();
  // if (R_FAILED(rc))
  //   diagAbortWithResult(MAKERESULT(Module_Libnx,
  //   LibnxError_ShouldNotHappen));

  smExit();
}

void __appExit(void) {
  hidExit();
  socketExit();
}

/*
Thread sEventWaitThread;
Thread *sMainThread;

static void eventWaitThreadFunc(void *data) {
  Event *event = appletGetMessageEvent();

  while (true) {
    eventWait(event, UINT64_MAX);
    u32 msg = -1;
    if (R_SUCCEEDED(appletGetMessage(&msg)) && msg == 31) {
      ApmPerformanceMode mode;
      apmGetPerformanceMode(&mode);

      if (mode == ApmPerformanceMode_Boost)
        threadPause(sMainThread);
      else
        threadResume(sMainThread);
    }
  }
}
*/

size_t getNumControllers() {
  size_t numControllers = 0;
  for (HidNpadIdType i = HidNpadIdType_No1; i <= HidNpadIdType_No8; i++) {
    if (hidGetNpadStyleSet((HidNpadIdType)i) != 0)
      numControllers++;
  }
  return numControllers;
}

// Main program entrypoint
int main(int argc, char *argv[]) {
  // Initialization code can go here.

  // sMainThread = threadGetSelf();
  //
  // const size_t stackSize = 0x1000;
  // void *stackMem = malloc(stackSize);
  // threadCreate(&sEventWaitThread, eventWaitThreadFunc, NULL, stackMem,
  //             stackSize, 16, -2);
  // threadStart(&sEventWaitThread);

  ViDisplay display;
  Event displayEvent;

  padConfigureInput(1, HidNpadStyleSet_NpadStandard);
  PadState pad;
  padInitializeAny(&pad);
restartSocket:
  viOpenDefaultDisplay(&display);
  viGetDisplayVsyncEvent(&display, &displayEvent);

  {}
  int sockfd = socket(AF_INET, SOCK_DGRAM, 0);
  if (sockfd < 0) {
    printf("socket creation failed\n");
    return -1;
  }

  struct sockaddr_in servaddr, cliaddr;
  memset(&servaddr, 0, sizeof(servaddr));
  memset(&cliaddr, 0, sizeof(cliaddr));

  servaddr.sin_family = AF_INET;
  servaddr.sin_addr.s_addr = INADDR_ANY;
  servaddr.sin_port = htons(44302);

  if (bind(sockfd, (const struct sockaddr *)&servaddr, sizeof(servaddr)) < 0) {
    printf("bind failed");
    return -1;
  }

  unsigned int len = sizeof(cliaddr), n;

  struct {
    int numControllers;
    struct {
      u64 keys;
      HidAnalogStickState lPos;
      HidAnalogStickState rPos;
      HidSixAxisSensorState states[2];
      HidNpadControllerColor colors[2];
      HidNpadStyleTag style;
    } controllers[8];
  } packetData;

  HidSixAxisSensorHandle handlesFullKey[8];
  HidSixAxisSensorHandle handlesJoyDual[16];
  for (int i = 0; i < 8; i++)
    hidGetSixAxisSensorHandles(&handlesFullKey[i], 1, (HidNpadIdType)i,
                               HidNpadStyleTag_NpadFullKey);
  for (int i = 0; i < 8; i++)
    hidGetSixAxisSensorHandles(&handlesJoyDual[i * 2], 2, (HidNpadIdType)i,
                               HidNpadStyleTag_NpadJoyDual);
  for (int i = 0; i < 8; i++)
    hidStartSixAxisSensor(handlesFullKey[i]);
  for (int i = 0; i < 16; i++)
    hidStartSixAxisSensor(handlesJoyDual[i]);

  struct {
    u32 packetsPerSecond;
  } settings;
  settings.packetsPerSecond = 60;

  unsigned char buffer[32] = {0};

  while (true) {
    packetData.numControllers = getNumControllers();
    for (int i = 0; i < packetData.numControllers; i++) {
      if (settings.packetsPerSecond == UINT32_MAX) {
        eventWait(&displayEvent, UINT64_MAX);
      } else
        svcSleepThread(1000000000 / settings.packetsPerSecond);

      packetData.controllers[i].style = hidGetNpadStyleSet((HidNpadIdType)i);

      HidNpadCommonState state;
      if (packetData.controllers[i].style & HidNpadStyleTag_NpadFullKey)
        hidGetNpadStatesFullKey((HidNpadIdType)i, &state, 1);
      else if (packetData.controllers[i].style & HidNpadStyleTag_NpadJoyDual)
        hidGetNpadStatesJoyDual((HidNpadIdType)i, &state, 1);
      else if (packetData.controllers[i].style & HidNpadStyleTag_NpadHandheld)
        hidGetNpadStatesHandheld(HidNpadIdType_Handheld, &state, 1);
      else if (packetData.controllers[i].style & HidNpadStyleTag_NpadJoyLeft)
        hidGetNpadStatesJoyLeft((HidNpadIdType)i, &state, 1);
      else if (packetData.controllers[i].style & HidNpadStyleTag_NpadJoyRight)
        hidGetNpadStatesJoyRight((HidNpadIdType)i, &state, 1);

      packetData.controllers[i].keys = state.buttons;
      packetData.controllers[i].lPos = state.analog_stick_l;
      packetData.controllers[i].rPos = state.analog_stick_r;

      if (packetData.controllers[i].style & HidNpadStyleTag_NpadJoyDual) {
        hidGetSixAxisSensorStates(handlesJoyDual[i * 2],
                                  &packetData.controllers[i].states[0], 1);
        hidGetSixAxisSensorStates(handlesJoyDual[i * 2 + 1],
                                  &packetData.controllers[i].states[1], 1);
        hidGetNpadControllerColorSplit((HidNpadIdType)i,
                                       &packetData.controllers[i].colors[0],
                                       &packetData.controllers[i].colors[1]);
      } else {
        hidGetSixAxisSensorStates(handlesFullKey[i],
                                  packetData.controllers[i].states, 1);
        hidGetNpadControllerColorSingle((HidNpadIdType)i,
                                        &packetData.controllers[i].colors[0]);

        // ApmPerformanceMode mode;
        // apmGetPerformanceMode(&mode);
        // if (mode == ApmPerformanceMode_Boost) {
        //   packetData.colors[0].main = 0xff000000;
        //   packetData.colors[0].sub = 0xff000000;
        // }
      }

      u64 k = packetData.controllers[i].keys;
      if (k & HidNpadButton_L && k & HidNpadButton_ZL && k & HidNpadButton_R &&
          k & HidNpadButton_ZR && k & HidNpadButton_Plus) {
        close(sockfd);
        goto restartSocket;
      }
    }

    n = recvfrom(sockfd, buffer, sizeof(buffer), MSG_DONTWAIT,
                 (struct sockaddr *)&cliaddr, &len);
    if (n > 0)
      switch (*(int32_t *)&buffer[0]) {
      case 0: { // Hello packet
        break;
      }
      case 1: { // Settings packet
        settings.packetsPerSecond = *(int32_t *)(&buffer[sizeof(int32_t)]);
        break;
      }
      default:
        break;
      }

    sendto(sockfd, &packetData,
           sizeof(packetData.numControllers) +
               packetData.numControllers * sizeof(packetData.controllers[0]),
           0, (const struct sockaddr *)&cliaddr, len);
  }

  // Your code / main loop goes here.
  // If you need threads, you can use threadCreate etc.

  viCloseDisplay(&display);
  close(sockfd);
  // Deinitialization and resources clean up code can go here.
  return 0;
}
