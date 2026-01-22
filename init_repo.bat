@echo off
echo Initializing and updating submodules...
git submodule update --init --recursive

echo Copying WebSocket directory...
xcopy "Dependency\NativeWebSocket\NativeWebSocket\Assets\WebSocket" "Dependency\WebSocket" /E /I /Y /Q

echo Deleting NativeWebSocket repository...
rmdir /s /q "Dependency\NativeWebSocket"

echo Done.
pause
