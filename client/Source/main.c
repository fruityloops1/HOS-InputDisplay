#include "config.h"
#include "hid.h"
#include "raylib.h"
#include "socketshit.h"
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

  while (1) {
    updateSocketShit();

    // printf("%zu %d %d %d %d\n", getPacketData()->keys,
    // getPacketData()->lPos.x,
    //        getPacketData()->lPos.y, getPacketData()->rPos.x,
    //        getPacketData()->rPos.y);
    BeginDrawing();

    ClearBackground(*(Color *)&cfg.colBg);

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