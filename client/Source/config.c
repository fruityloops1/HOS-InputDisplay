#include "config.h"
#include "ini.h"
#include "raylib.h"
#include <stdlib.h>
#include <string.h>

void hexColorToBytes(const char *color, unsigned char *out) {
  if (color[0] == '#') {
    color++;
  }

  for (int i = 0; i < 4; i++) {
    char byteString[3];
    strncpy(byteString, color + (i * 2), 2);
    byteString[2] = '\0';
    out[i] = (unsigned char)strtol(byteString, NULL, 16);
  }
}

CColor colorFromStr(const char *color) {
  unsigned char col[4];
  hexColorToBytes(color, col);
  CColor rayCol = {.r = col[0], .g = col[1], .b = col[2], .a = col[3]};
  return rayCol;
}

static int handler(void *user, const char *section, const char *name,
                   const char *value) {
  config *pconfig = (config *)user;

#define MATCH(s, n) strcmp(section, s) == 0 && strcmp(name, n) == 0
  if (MATCH("config", "packetsPerSecond")) {
    pconfig->packetsPerSecond = atoi(value);
  } else if (MATCH("config", "host")) {
    pconfig->host = strdup(value);
  } else if (MATCH("config", "fontPath")) {
    pconfig->fontPath = strdup(value);
  } else if (MATCH("colors", "inactive")) {
    pconfig->colInactive = colorFromStr(value);
  } else if (MATCH("colors", "active")) {
    pconfig->colActive = colorFromStr(value);
  } else if (MATCH("colors", "stick")) {
    pconfig->colStick = colorFromStr(value);
  } else if (MATCH("colors", "font")) {
    pconfig->colFont = colorFromStr(value);
  } else if (MATCH("colors", "background")) {
    pconfig->colBg = colorFromStr(value);
  } else {
    return 0; /* unknown section/name, error */
  }
  return 1;
}

config loadConfig() {
  config c;

  if (ini_parse("Res/config.ini", handler, &c) < 0) {
    printf("config epic fail !!!!");
    exit(-1);
  }

  return c;
}