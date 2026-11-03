
@echo off
setlocal

cd /d "%~dp0"

set APP_DIR=..\bin\Debug\net10.0
set TEST_DIR=..\..\ColdChainTests\bin\Debug\net10.0

if not exist "%APP_DIR%" mkdir "%APP_DIR%"
if not exist "%TEST_DIR%" mkdir "%TEST_DIR%"

cl /nologo /LD /O2 native_risk.c /link /OUT:"%APP_DIR%\NativeRisk.dll"

if errorlevel 1 (
    echo Native library build failed.
    exit /b 1
)

copy /Y "%APP_DIR%\NativeRisk.dll" "%TEST_DIR%\NativeRisk.dll"

if errorlevel 1 (
    echo Failed to copy DLL to test output.
    exit /b 1
)

echo NativeRisk.dll built and copied successfully.
exit /b 0
