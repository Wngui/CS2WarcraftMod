@echo off
:: Read paths from config.txt
for /f "tokens=1,* delims==" %%A in (config.txt) do set "%%A=%%B"

:: Construct the full path to cs2.exe
set "CS2_EXE=%BASE_PATH%\game\bin\win64\cs2.exe"

:: Run the command
start "" "%CS2_EXE%" -dedicated -autoupdate -maxplayers 64 -tickrate 128 +game_mode 0 +game_type 3 +map de_dust2
