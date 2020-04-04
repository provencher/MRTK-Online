#!/bin/bash

cd `dirname $0`/../Assets/

FOLDERS_TO_LINK=(
    MixedRealityToolkit
    MixedRealityToolkit.SDK
    MixedRealityToolkit.Services
    MixedRealityToolkit.Providers
    MixedRealityToolkit.Examples
    MixedRealityToolkit.Extensions
    MixedRealityToolkit.Tools
    MRTK
)

for folder in "${FOLDERS_TO_LINK[@]}"
do
ln -s ../External/MixedRealityToolkit-Unity/Assets/$folder $folder
ln -s ../External/MixedRealityToolkit-Unity/Assets/$folder.meta $folder.meta
done