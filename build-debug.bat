@echo off

if exist logs goto msbuild
mkdir logs

:msbuild
"%PROGRAMFILES(X86)%\MSBuild\14.0\Bin\MSBUILD.exe" HostsFileEditor.proj /t:Build /p:Configuration=Debug /l:FileLogger,Microsoft.Build.Engine;logfile=logs\build-debug.log