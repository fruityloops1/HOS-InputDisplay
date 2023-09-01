#ifndef CONFIG_H
#define CONFIG_H

typedef struct CColor {
    unsigned char r; // Color red value
    unsigned char g; // Color green value
    unsigned char b; // Color blue value
    unsigned char a; // Color alpha value
} CColor;

typedef struct {
    int packetsPerSecond;
    const char* host;
    const char* fontPath;
    const char* fatFontPath;
    CColor colInactive;
    CColor colActive;
    CColor colStick;
    CColor colFont;
    CColor colBg;
} config;

config loadConfig();

#endif