#include "config.h"
#include "hid.h"
#include "raylib.h"
#include "raymath.h"
#include "socketshit.h"
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define SWIDTH 619
#define SHEIGHT 360

config cfg;
Font buttonFont;

void drawStick(Vector2 pos, HidAnalogStickState stick, HidNpadButton button) {
  Vector2 at;
  at.x = pos.x + stick.x / 800;
  at.y = pos.y + -stick.y / 800;
  DrawRing(pos, 56, 50, 0, 360, 40, *(Color *)&cfg.colInactive);
  DrawCircleV(at, 40,
              getPacketData()->keys & button ? *(Color *)&cfg.colActive
                                             : *(Color *)&cfg.colStick);
}

Quaternion toQuaternion(float m[3][3]) {
  Quaternion q;

  float trace = m[0][0] + m[1][1] + m[2][2];
  if (trace > 0) {
    float s = 0.5f / sqrtf(trace + 1.0f);
    q.w = 0.25f / s;
    q.x = (m[2][1] - m[1][2]) * s;
    q.y = (m[0][2] - m[2][0]) * s;
    q.z = (m[1][0] - m[0][1]) * s;
  } else {
    if (m[0][0] > m[1][1] && m[0][0] > m[2][2]) {
      float s = 2.0f * sqrtf(1.0f + m[0][0] - m[1][1] - m[2][2]);
      q.w = (m[2][1] - m[1][2]) / s;
      q.x = 0.25f * s;
      q.y = (m[0][1] + m[1][0]) / s;
      q.z = (m[0][2] + m[2][0]) / s;
    } else if (m[1][1] > m[2][2]) {
      float s = 2.0f * sqrtf(1.0f + m[1][1] - m[0][0] - m[2][2]);
      q.w = (m[0][2] - m[2][0]) / s;
      q.x = (m[0][1] + m[1][0]) / s;
      q.y = 0.25f * s;
      q.z = (m[1][2] + m[2][1]) / s;
    } else {
      float s = 2.0f * sqrtf(1.0f + m[2][2] - m[0][0] - m[1][1]);
      q.w = (m[1][0] - m[0][1]) / s;
      q.x = (m[0][2] + m[2][0]) / s;
      q.y = (m[1][2] + m[2][1]) / s;
      q.z = 0.25f * s;
    }
  }

  return q;
}

void drawButton(Vector2 pos, float width, float height, HidNpadButton button,
                const char *label, float roundness, float fontOffsetX,
                float fontOffsetY, float fontSize) {
  Rectangle rec;
  rec.x = pos.x - width;
  rec.y = pos.y - height;
  rec.height = height * 2;
  rec.width = width * 2;
  DrawRectangleRounded(rec, roundness, 8,
                       getPacketData()->keys & button
                           ? *(Color *)&cfg.colActive
                           : *(Color *)&cfg.colInactive);
  pos.x += fontOffsetX;
  pos.y += fontOffsetY;
  DrawTextEx(buttonFont, label, pos, fontSize, 4, *(Color *)&cfg.colFont);
}

int main() {
  cfg = loadConfig();
  initSocketShit(cfg);
  InitWindow(SWIDTH, SHEIGHT, "Horizon-InputDisplay");
  SetConfigFlags(FLAG_MSAA_4X_HINT);

  buttonFont = LoadFontEx(cfg.fontPath, 38, 0, 250);

  Camera3D camera = {0};
  camera.projection = CAMERA_PERSPECTIVE;
  camera.fovy = 45;
  camera.target.x = 0;
  camera.target.y = 0;
  camera.target.z = 0;
  camera.up.x = 0;
  camera.up.y = 1;
  camera.up.z = 0;
  camera.position.x = 0;
  camera.position.y = -15;
  camera.position.z = 15;
  Model cube = LoadModel("Res/Vallarta_Christopher_HW2_Controller.obj");

  Vector3 cubePos;
  cubePos.x = 0;
  cubePos.y = 0;
  cubePos.z = 0;

  Quaternion baseRotation;
  baseRotation.x = 0;
  baseRotation.y = 0;
  baseRotation.z = 0;
  baseRotation.w = 1;
  int cooldown = -1;

  while (1) {
    updateSocketShit();

    BeginDrawing();
    BeginMode3D(camera);

    ClearBackground(*(Color *)&cfg.colBg);

    Quaternion quat = toQuaternion(getPacketData()->state.direction.direction);
    quat.x *= -1;
    quat.y *= -1;
    quat.z *= -1;
    cube.transform = QuaternionToMatrix(
        QuaternionMultiply(QuaternionInvert(baseRotation), quat));
    if (cooldown > 0)
      cooldown--;
    if (getPacketData()->keys & HidNpadButton_StickL && cooldown <= 0) {
      baseRotation = toQuaternion(getPacketData()->state.direction.direction);
      baseRotation.x *= -1;
      baseRotation.y *= -1;
      baseRotation.z *= -1;
      cooldown = 60;
    }
    DrawModel(cube, cubePos, 2, WHITE);
    EndMode3D();

    Vector2 pos;
    pos.x = 100;
    pos.y = 180;
    drawStick(pos, getPacketData()->lPos, HidNpadButton_StickL);

    pos.x = 380;
    pos.y = 270;
    drawStick(pos, getPacketData()->rPos, HidNpadButton_StickR);

    pos.x = 550;
    pos.y = 180;
    drawButton(pos, 25, 25, HidNpadButton_A, "A", 1, -12, -19, 38);

    pos.x = 450;
    pos.y = 180;
    drawButton(pos, 25, 25, HidNpadButton_Y, "Y", 1, -12, -19, 38);

    pos.x = 500;
    pos.y = 230;
    drawButton(pos, 25, 25, HidNpadButton_B, "B", 1, -12, -19, 38);

    pos.x = 500;
    pos.y = 130;
    drawButton(pos, 25, 25, HidNpadButton_X, "X", 1, -12, -19, 38);

    pos.x = 380;
    pos.y = 130;
    drawButton(pos, 12, 12, HidNpadButton_Plus, "+", 1, -9, -19, 38);

    pos.x = 220;
    pos.y = 130;
    drawButton(pos, 12, 12, HidNpadButton_Minus, "-", 1, -7, -21, 38);

    pos.x = 500;
    pos.y = 70;
    drawButton(pos, 40, 15, HidNpadButton_R, "R", 1, -12, -20, 38);

    pos.x = 500;
    pos.y = 30;
    drawButton(pos, 40, 15, HidNpadButton_ZR, "ZR", 1, -22, -19, 38);

    pos.x = 100;
    pos.y = 70;
    drawButton(pos, 40, 15, HidNpadButton_L, "L", 1, -12, -20, 38);

    pos.x = 100;
    pos.y = 30;
    drawButton(pos, 40, 15, HidNpadButton_ZL, "ZL", 1, -22, -19, 38);

    pos.x = 220;
    pos.y = 240;
    drawButton(pos, 15, 15, HidNpadButton_Up, "", .5, 0, 0, 38);
    pos.x = 220;
    pos.y = 300;
    drawButton(pos, 15, 15, HidNpadButton_Down, "", .5, 0, 0, 38);
    pos.x = 190;
    pos.y = 270;
    drawButton(pos, 15, 15, HidNpadButton_Left, "", .5, 0, 0, 38);
    pos.x = 250;
    pos.y = 270;
    drawButton(pos, 15, 15, HidNpadButton_Right, "", .5, 0, 0, 38);

    pos.x = 220;
    pos.y = 270;
    Rectangle rec;
    rec.x = pos.x - 20;
    rec.y = pos.y - 15;
    rec.height = 15 * 2;
    rec.width = 20 * 2;
    DrawRectangleRounded(rec, 0, 8, *(Color *)&cfg.colInactive);
    rec.x = pos.x - 15;
    rec.y = pos.y - 20;
    rec.height = 20 * 2;
    rec.width = 15 * 2;
    DrawRectangleRounded(rec, 0, 8, *(Color *)&cfg.colInactive);

    EndDrawing();
  }

  free((void *)cfg.host);
  free((void *)cfg.fontPath);
  return 0;
}