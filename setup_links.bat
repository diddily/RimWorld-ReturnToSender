@echo off
set /p rpath="Path to Rimworld: "
set /p hpath="Path to HugsLib: "

mkdir "..\ReturnToSender - Release"
mkdir "..\ReturnToSender - Debug"
mklink /J "%rpath%\Mods\ReturnToSender - Release" "..\ReturnToSender - Release"
mklink /J "%rpath%\Mods\ReturnToSender - Debug" "..\ReturnToSender - Debug"
mklink /J "RimWorldAssemblies\" "%rpath%\RimWorldWin64_Data\Managed"
mklink /J "HugsLibsAssemblies\" "%hpath%\Assemblies"