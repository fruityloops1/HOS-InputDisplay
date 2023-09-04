#include "switch/kernel/svc.h"
#include "switch/runtime/pad.h"
#include "switch/services/hid.h"
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
  smExit();
}

void __appExit(void) {
  hidExit();
  socketExit();
}

// Main program entrypoint
int main(int argc, char *argv[]) {
  // Initialization code can go here.

  padConfigureInput(1, HidNpadStyleSet_NpadStandard);
  PadState pad;
  padInitializeAny(&pad);
restartSocket :

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
    u64 keys;
    HidAnalogStickState lPos;
    HidAnalogStickState rPos;
    HidSixAxisSensorState states[2];
    HidNpadControllerColor colors[2];
    HidNpadStyleTag style;
  } packetData;

  typeof(packetData) lastData;

  HidSixAxisSensorHandle handleFullKey;
  HidSixAxisSensorHandle handleJoyDual[2];
  hidGetSixAxisSensorHandles(&handleFullKey, 1, HidNpadIdType_No1,
                             HidNpadStyleTag_NpadFullKey);
  hidGetSixAxisSensorHandles(handleJoyDual, 2, HidNpadIdType_No1,
                             HidNpadStyleTag_NpadJoyDual);
  hidStartSixAxisSensor(handleFullKey);
  hidStartSixAxisSensor(handleJoyDual[0]);
  hidStartSixAxisSensor(handleJoyDual[1]);

  struct {
    int packetsPerSecond;
  } settings;
  settings.packetsPerSecond = 60;

  unsigned char buffer[32] = {0};

  while (true) {
    svcSleepThread(1000000000 / settings.packetsPerSecond);

    lastData = packetData;
    padUpdate(&pad);
    packetData.keys = padGetButtons(&pad);
    packetData.lPos = padGetStickPos(&pad, 0);
    packetData.rPos = padGetStickPos(&pad, 1);
    packetData.style = hidGetNpadStyleSet(HidNpadIdType_No1);

    if (packetData.style & HidNpadStyleTag_NpadJoyDual) {
      hidGetSixAxisSensorStates(handleJoyDual[0], &packetData.states[0], 1);
      hidGetSixAxisSensorStates(handleJoyDual[1], &packetData.states[1], 1);
      hidGetNpadControllerColorSplit(HidNpadIdType_No1, &packetData.colors[0],
                                     &packetData.colors[1]);
    } else {

      hidGetSixAxisSensorStates(handleFullKey, packetData.states, 1);
      hidGetNpadControllerColorSingle(HidNpadIdType_No1, &packetData.colors[0]);
    }

    u64 k = packetData.keys;
    if (k & HidNpadButton_L && k & HidNpadButton_ZL && k & HidNpadButton_R &&
        k & HidNpadButton_ZR && k & HidNpadButton_Plus) {
      close(sockfd);
      goto restartSocket;
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

    sendto(sockfd, &packetData, sizeof(packetData), 0,
           (const struct sockaddr *)&cliaddr, len);
    // if (packetData.keys == lastData.keys &&
    //     packetData.lPos.x == lastData.lPos.x &&
    //     packetData.lPos.y == lastData.lPos.y &&
    //     packetData.rPos.x == lastData.rPos.x &&
    //     packetData.rPos.y == lastData.rPos.y) {
    // } else {
    // }
  }

  // Your code / main loop goes here.
  // If you need threads, you can use threadCreate etc.

  close(sockfd);
  // Deinitialization and resources clean up code can go here.
  return 0;
}
