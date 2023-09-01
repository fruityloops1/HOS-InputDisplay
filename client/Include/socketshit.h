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

/// HidDirectionState
typedef struct HidDirectionState {
    float direction[3][3]; ///< 3x3 matrix
} HidDirectionState;

/// HidVector
typedef struct HidVector {
    float x;
    float y;
    float z;
} HidVector;

/// HidSixAxisSensorState
typedef struct HidSixAxisSensorState {
    uint64_t delta_time; ///< DeltaTime
    uint64_t sampling_number; ///< SamplingNumber
    HidVector acceleration; ///< Acceleration
    HidVector angular_velocity; ///< AngularVelocity
    HidVector angle; ///< Angle
    HidDirectionState direction; ///< Direction
    uint32_t attributes; ///< Bitfield of \ref HidSixAxisSensorAttribute.
    uint32_t reserved; ///< Reserved
} HidSixAxisSensorState;

typedef struct PacketData {
    uint64_t keys;
    HidAnalogStickState lPos;
    HidAnalogStickState rPos;
    HidSixAxisSensorState state;
} PacketData;

void initSocketShit(config cfg);
void updateSocketShit();
void exitSocketShit();
PacketData* getPacketData();

#endif