@echo off

if exist logs goto msbuild
mkdir logs

:msbuild
%windir%\Microsoft.NET\Framework\v4.0.30319\MSBUILD.exe HostsFileEditor.proj /t:Clean