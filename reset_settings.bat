@echo off
echo Resetting CK3 Reader settings...
echo.
echo This will delete all saved settings including:
echo - API keys
echo - Voice configurations
echo - Volume/speed settings
echo.
pause

rd /s /q "C:\Users\jack\AppData\Local\CK3_Reader"

echo.
echo Settings have been reset!
echo The app will create fresh settings with default voices (Main, Sarah, Clyde) on next launch.
echo.
pause
