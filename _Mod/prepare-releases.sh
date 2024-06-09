#!/bin/bash
if [ -z "$1" ]
then
  echo "Hey, give a version number!"
  exit
fi

cd ../DeepStorage
#....fuuuuuuuuuuck, msbuild command line is for whatever reason copying over the package references, and
# we can't have that. So fuck this whole thing anyway
#msbuild
cd ../_Mod
zip -r LWM-DeepStorage-Debug.$1.zip LWM.DeepStorage
cd ../DeepStorage
#msbuild '/p:Configuration=Release'
cd ../_Mod
zip -r LWM-DeepStorage.$1.zip LWM.DeepStorage

echo "NOTE: IF YOU DID NOT BUILD THIS IN MONODEVELOP YOU HAVE PROBLEMS!"



