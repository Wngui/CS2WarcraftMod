@echo off
:: Paths

set "STEAMCMD_PATH=C:\cs2-server\steamcmd.exe" & :: <-- Change path
set "BASE_PATH=C:\cs2-server\cs2-ds" & :: <-- Change path


set "CSGO_PATH=%BASE_PATH%\game\csgo"
set "CFG_PATH=%CSGO_PATH%\cfg"
set "DOWNLOAD_DIR=%BASE_PATH%\downloads"

:: Specific file paths
set "SERVER_CFG_PATH=%CFG_PATH%\server.cfg"
set "SERVER_BACKUP_PATH=%CFG_PATH%\server_backup.cfg"
set "GAMEINFO_FILE=%CSGO_PATH%\gameinfo.gi"

:: Backup server.cfg
echo Backing up server.cfg...
copy /Y "%SERVER_CFG_PATH%" "%SERVER_BACKUP_PATH%"

:: Update CS2
echo Updating CS2...
"%STEAMCMD_PATH%" +login anonymous +force_install_dir "%BASE_PATH%" +app_update 730 validate +quit

:: Create download folder if does not exist
if not exist "%DOWNLOAD_DIR%" mkdir "%DOWNLOAD_DIR%"

:: Update Metamod
echo Updating metamod...
for /f "delims=" %%A in ('curl -s https://mms.alliedmods.net/mmsdrop/2.0/mmsource-latest-windows') do set "latestDownload=%%A"
curl -o "%DOWNLOAD_DIR%\%latestDownload%" "https://mms.alliedmods.net/mmsdrop/2.0/%latestDownload%"
echo Installing metamod version %latestDownload%...
tar -xf "%DOWNLOAD_DIR%\%latestDownload%" -C "%CSGO_PATH%"
del "%DOWNLOAD_DIR%\%latestDownload%"

:: Remove gameinfo entries
echo Removing old gameinfo entries...
powershell -Command "(Get-Content '%GAMEINFO_FILE%') -notmatch '^\t\t\tGame\tcsgo\\addons\\metamod' | Set-Content '%GAMEINFO_FILE%'"

:: Re-add gameinfo entries
echo Re-adding gameinfo entries...
powershell -Command "(Get-Content '%GAMEINFO_FILE%') -replace '(Game\tcsgo\s)', '`t`t`tGame\tcsgo\\addons\\metamod`n$1' | Set-Content '%GAMEINFO_FILE%'"

:: Update CounterStrikeSharp
echo Updating CounterStrikeSharp...
set "latestDownload="
for /f "delims=" %%i in ('curl -s https://api.github.com/repos/roflmuffin/CounterStrikeSharp/releases/latest ^| findstr "browser_download_url" ^| findstr "with-runtime-build-.*-windows"') do set "latestDownload=%%i"
set "latestDownload=%latestDownload:*: =%"
echo Installing CounterStrikeSharp: %latestDownload%
set "zipFile=counterstrikesharp"
curl -L -o "%DOWNLOAD_DIR%\%zipFile%" "%latestDownload%"
echo Installing CounterStrikeSharp version %zipFile%...
tar -xf "%DOWNLOAD_DIR%\%zipFile%" -C "%CSGO_PATH%"
del "%DOWNLOAD_DIR%\%zipFile%"

:: Restore server.cfg
echo Restoring server.cfg...
copy /Y "%SERVER_BACKUP_PATH%" "%SERVER_CFG_PATH%"

echo Done!
pause
