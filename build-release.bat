@echo off

if exist logs goto msbuild
mkdir logs

:msbuild
%windir%\Microsoft.NET\Framework\v4.0.30319\MSBUILD.exe HostsFileEditor.proj /t:Build /p:Configuration=Release /l:FileLogger,Microsoft.Build.Engine;logfile=logs\build-release.log