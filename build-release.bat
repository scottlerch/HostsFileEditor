@echo off

if "%1"=="" goto passwordblank
if exist logs goto msbuild
mkdir logs

:msbuild
"%PROGRAMFILES(X86)%\MSBuild\14.0\Bin\MSBUILD.exe" HostsFileEditor.proj /t:Build /p:Configuration=Release /p:Sign=true /p:CertPassword=%1 /l:FileLogger,Microsoft.Build.Engine;logfile=logs\build-release.log
goto end

:passwordblank
echo Must specify certificate password

:end