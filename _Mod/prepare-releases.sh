#!/bin/bash
if [ -z "$1" ]
then
  echo "Hey, give a version number!"
  exit
fi

cd ../DeepStorage
msbuild
cd ../_Mod
zip -r LWM-DeepStorage-Debug.$1.zip LWM.DeepStorage
cd ../DeepStorage
msbuild '/p:Configuration=Release'
cd ../_Mod
zip -r LWM-DeepStorage.$1.zip LWM.DeepStorage



