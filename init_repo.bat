@echo off
setlocal EnableExtensions

REM Always run from the repo root (the directory of this script)
cd /d "%~dp0"

REM Avoid calling a git.bat wrapper (e.g. depot_tools\git.bat) which can terminate cmd.exe.
set "GIT_EXE="
for %%G in (git.exe) do set "GIT_EXE=%%~$PATH:G"
if not defined GIT_EXE (
	echo ERROR: git.exe not found in PATH.
	echo Please install Git for Windows or add it to PATH.
	pause
	exit /b 1
)

echo Initializing and updating submodules...
"%GIT_EXE%" submodule update --init --recursive
if %ERRORLEVEL% NEQ 0 (
	echo ERROR: Failed to update submodules.
	pause
	exit /b %ERRORLEVEL%
)

echo Copying WebSocket directory...
if not exist "Dependency\NativeWebSocket\NativeWebSocket\Assets\WebSocket" (
	echo ERROR: Source directory not found:
	echo   Dependency\NativeWebSocket\NativeWebSocket\Assets\WebSocket
	pause
	exit /b 1
)

xcopy "Dependency\NativeWebSocket\NativeWebSocket\Assets\WebSocket" "Dependency\WebSocket" /E /I /Y /Q
if %ERRORLEVEL% NEQ 0 (
	echo ERROR: Failed to copy WebSocket directory.
	pause
	exit /b %ERRORLEVEL%
)

echo Deleting NativeWebSocket repository...
if exist "Dependency\NativeWebSocket" (
	rmdir /s /q "Dependency\NativeWebSocket"
)

echo Done.
pause
