@echo off

if "%1"=="" goto passwordblank
if exist logs goto msbuild
mkdir logs

:msbuild
"%PROGRAMFILES(X86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -property installationPath > vspath.txt
set /p VSPath= < vspath.txt
del vspath.txt

"%VSPath%\MSBuild\Current\Bin\MSBUILD.exe" HostsFileEditor.proj /t:Build /p:Configuration=Release /p:StrongName=true /p:Sign=true /p:CertPassword=%1 /l:FileLogger,Microsoft.Build.Engine;logfile=logs\build-release.log
goto end

:passwordblank
echo Must specify certificate password

:end