#include <stdio.h>
#ifdef _WIN32
#include <winsock2.h>
#pragma comment(lib, "ws2_32.lib")
#else
#include <arpa/inet.h>
#include <sys/socket.h>
#include <unistd.h>
#endif

#include "socketshit.h"
#include <stdlib.h>
#include <string.h>

int sockfd;
struct sockaddr_in servaddr;
unsigned int len = sizeof(servaddr);

void initSocketShit(config cfg) {

#ifdef _WIN32
  WSADATA wsaData;
  if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
    perror("WSAStartup failed");
    exit(-1);
  }
#endif

  // Creating socket file descriptor
  if ((sockfd = socket(AF_INET, SOCK_DGRAM, 0)) < 0) {
    perror("socket creation failed");
#ifdef _WIN32
    WSACleanup();
#endif
    exit(-1);
  }

  memset(&servaddr, 0, sizeof(servaddr));

  // Filling server information
  servaddr.sin_family = AF_INET;
  servaddr.sin_port = htons(PORT);
  servaddr.sin_addr.s_addr = inet_addr(cfg.host);

  unsigned int helloPacket[] = {0};
  sendto(sockfd, helloPacket, sizeof(helloPacket), 0,
         (struct sockaddr *)&servaddr, len);

  unsigned int configPacket[] = {1, cfg.packetsPerSecond};
  sendto(sockfd, configPacket, sizeof(configPacket), 0,
         (struct sockaddr *)&servaddr, len);
}

PacketData packetData;
PacketData lastPacketData;
PacketData *getPacketData() { return &packetData; }
PacketData *getLastPacketData() { return &lastPacketData; }

void updateSocketShit() {
  lastPacketData = packetData;
  recvfrom(sockfd, (char *)&packetData, sizeof(packetData), MSG_WAITALL,
           (struct sockaddr *)&servaddr, &len);
}

void exitSocketShit() {
#ifdef _WIN32
  closesocket(sockfd);
  WSACleanup();
#else
  close(sockfd);
#endif
}
