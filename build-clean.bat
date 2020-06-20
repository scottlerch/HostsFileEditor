@echo off

if exist logs goto msbuild
mkdir logs

:msbuild
"%PROGRAMFILES(X86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -property installationPath > vspath.txt
set /p VSPath= < vspath.txt
del vspath.txt

"%VSPath%\MSBuild\Current\Bin\MSBUILD.exe" HostsFileEditor.proj /t:Clean