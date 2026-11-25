#!/bin/sh
# Buduje JustInstall.exe (.NET Framework 4.8 – wbudowany w Windows 10/11). Wymaga: mono (mcs).
set -e
cd "$(dirname "$0")"
mkdir -p dist
mcs -nologo -target:winexe -optimize+ -langversion:7 -out:dist/JustInstall.exe \
  -win32icon:assets/icon.ico -win32manifest:app.manifest \
  -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Web.Extensions.dll \
  -resource:assets/justinstall-128.png,justinstall-128.png \
  -resource:assets/justmusic-128.png,justmusic-128.png \
  -resource:assets/justminecraft-128.png,justminecraft-128.png \
  src/*.cs
cp dist/JustInstall.exe JustInstall.exe
ls -la JustInstall.exe
