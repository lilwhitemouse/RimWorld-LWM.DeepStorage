#!/bin/bash
if [ -z "$1" ]
then
  echo "Hey, give a version number!"
  exit
fi

cd ../DeepStorage
#....fuuuuuuuuuuck, building is for whatever reason copying over the package references, and
# we can't have that. So fuck this whole thing anyway
msbuild
cd ../_Mod
rm LWM.DeepStorage/1.5/Assemblies/0*
rm LWM.DeepStorage/1.5/Assemblies/H*
zip -r LWM-DeepStorage-Debug.$1.zip LWM.DeepStorage
cd ../DeepStorage
msbuild '/p:Configuration=Release'
cd ../_Mod
rm LWM.DeepStorage/1.5/Assemblies/0*
rm LWM.DeepStorage/1.5/Assemblies/H*
zip -r LWM-DeepStorage.$1.zip LWM.DeepStorage

echo "CHECK ASSEMBLIES!!"

