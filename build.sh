#!/bin/bash

PROJ="OJDS-NX"
AUTH="Nichole Mattera"
TAGY="${1:-v0.0.1}"
VERS="${TAGY:1}"

OUT="./out/atmosphere/contents/0100000000000901"
mkdir  -p  $OUT/flags/

docker  run  --rm         \
  --pull=always           \
  -u $(id -u):$(id -g)    \
  -v "/$PWD/":/OJDS-NX/   \
  -w /OJDS-NX/            \
  -e APP_TITLE="$PROJ"    \
  -e APP_AUTHOR="$AUTH"   \
  -e APP_VERSION="$VERS"  \
  devkitpro/devkita64     \
  make                    \
;

echo  '.'                 >$OUT/flags/boot2.flag
cp  ./source/toolbox.json  $OUT/toolbox.json
cp  ./out/OJDS-NX.nsp      $OUT/exefs.nsp

ZIP="$PROJ-$TAGY.zip"
cd ./out/
rm  -rf  "$ZIP"
zip  -rm9  "$ZIP"  ./atmosphere/
