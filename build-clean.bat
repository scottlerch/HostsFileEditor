@echo off
setlocal

:: Ensure logs folder exists
if not exist logs (
    mkdir logs
)

:: Locate vswhere
set VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe

:: Use vswhere to find latest MSBuild.exe
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
    set "MSBUILD=%%i"
)

:: Run MSBuild
if defined MSBUILD (
    "%MSBUILD%" HostsFileEditor.proj /t:Clean
) else (
    echo MSBuild.exe not found. Make sure Visual Studio is installed.
    exit /b 1
)

endlocal
