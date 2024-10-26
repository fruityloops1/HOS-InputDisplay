# HOS-InputDisplay

# This project has been abandoned, if you wish to still use it, please use the 1.3.2 version from the Releases tab, as the latest one is broken.

Nintendo switch input display meant as a replacement for OJDS-NX, because OJDS-NX causes load times to increase in some games. This can still be the case with this input display, but it is minimal.

## Build (Sysmodule)
Use devkitPro and make to build

## Build (Display)
Build with CMake, use MSys2 on Windows

## Installation
Place the .nsp in /atmosphere/contents/0100000000000901/exefs.nsp, the toolbox.json in /atmosphere/contents/0100000000000901/toolbox.json, and create /atmosphere/0100000000000901/flags/boot2.flag

# Usage
Put your switch IP in config.ini, and run the display program (make sure the sysmodule is running on your switch).
Press L + ZL + ZR + R + Plus to restart the input server.
