#!/bin/bash

PROJ="OJDS-NX"
AUTH="Nichole Mattera"
TAGY="${1:-v0.0.1}"
VERS="${TAGY:1}"

OUT="./out/athmosphere/contents/0100000000000901"
mkdir  -p  $OUT/flags/

export DOCKER_BUILDKIT=1
docker  build  .  -t ojds-nx-build
docker  run  --rm         \
  -u $(id -u):$(id -g)    \
  -v "/$PWD/":/OJDS-NX/   \
  -w /OJDS-NX/            \
  -e APP_TITLE="$PROJ"    \
  -e APP_AUTHOR="$AUTH"   \
  -e APP_VERSION="$VERS"  \
  ojds-nx-build           \
;
docker  rmi  ojds-nx-build

echo  '.'                 >$OUT/flags/boot2.flag
cp  ./source/toolbox.json  $OUT/toolbox.json
cp  ./out/OJDS-NX.nsp      $OUT/exefs.nsp

ZIP="$PROJ-$TAGY.zip"
cd ./out/
rm  -rf  "$ZIP"
zip  -rm9  "$ZIP"  ./athmosphere/
