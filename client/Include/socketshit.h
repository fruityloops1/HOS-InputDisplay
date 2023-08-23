#ifndef SOCKETSHIT_H
#define SOCKETSHIT_H

#include "config.h"
#define PORT 44302

/// HidAnalogStickState
#include <stdint.h>
typedef struct HidAnalogStickState {
    int32_t x; ///< X
    int32_t y; ///< Y
} HidAnalogStickState;

typedef struct PacketData {
    uint64_t keys;
    HidAnalogStickState lPos;
    HidAnalogStickState rPos;
} PacketData;

void initSocketShit(config cfg);
void updateSocketShit();
void exitSocketShit();
PacketData* getPacketData();

#endif