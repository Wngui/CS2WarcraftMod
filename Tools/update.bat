@echo off
:: Read paths from config.txt
for /f "tokens=1,* delims==" %%A in (config.txt) do set "%%A=%%B"

:: Display variables (for testing)
echo STEAMCMD_PATH=%STEAMCMD_PATH%
echo BASE_PATH=%BASE_PATH%

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

:: Add metamod line to gameinfo.gi
powershell -Command "(Get-Content $env:GAMEINFO_FILE) | Where-Object { $_ -notmatch \".*csgo/addons/metamod.*\" } | Set-Content $env:GAMEINFO_FILE"
powershell -Command "(Get-Content $env:GAMEINFO_FILE) -replace \"(Game_LowViolence.*)\", \"`$1`r`n`t`t`tGame`tcsgo/addons/metamod\" | Set-Content $env:GAMEINFO_FILE"

:: Create download folder if does not exist
if not exist "%DOWNLOAD_DIR%" mkdir "%DOWNLOAD_DIR%"

:: Update Metamod
echo Updating metamod...
for /f "delims=" %%A in ('curl -s https://mms.alliedmods.net/mmsdrop/2.0/mmsource-latest-windows') do set "latestDownload=%%A"
curl -o "%DOWNLOAD_DIR%\%latestDownload%" "https://mms.alliedmods.net/mmsdrop/2.0/%latestDownload%"
echo Installing metamod version %latestDownload%...
tar -xf "%DOWNLOAD_DIR%\%latestDownload%" -C "%CSGO_PATH%"
del "%DOWNLOAD_DIR%\%latestDownload%"

:: Update CounterStrikeSharp
echo Updating CounterStrikeSharp...
set "latestDownload="
for /f "delims=" %%i in ('curl -s https://api.github.com/repos/roflmuffin/CounterStrikeSharp/releases/latest ^| findstr "browser_download_url" ^| findstr "with-runtime-windows"') do set "latestDownload=%%i"
set "latestDownload=%latestDownload:*: =%"
echo Downloading CounterStrikeSharp: %latestDownload%
set "zipFile=counterstrikesharp"
curl -L -o "%DOWNLOAD_DIR%\%zipFile%" "%latestDownload%"
echo Installing CounterStrikeSharp version %latestDownload%...
tar -xf "%DOWNLOAD_DIR%\%zipFile%" -C "%CSGO_PATH%"
del "%DOWNLOAD_DIR%\%zipFile%"

:: Restore server.cfg
echo Restoring server.cfg...
copy /Y "%SERVER_BACKUP_PATH%" "%SERVER_CFG_PATH%"

:: Updating Warcraft
echo Updating Warcraft...
set "latestDownload="
for /f "delims=" %%i in ('curl -s https://api.github.com/repos/Wngui/CS2WarcraftMod/releases/latest ^| findstr "browser_download_url" ^| findstr "warcraft-plugin-.* "') do set "latestDownload=%%i"
set "latestDownload=%latestDownload:*: =%"
echo Downloading Warcraft: %latestDownload%
set "zipFile=warcraft"
curl -L -o "%DOWNLOAD_DIR%\%zipFile%" "%latestDownload%"
echo Installing Warcraft version %latestDownload%...
tar -xf "%DOWNLOAD_DIR%\%zipFile%" -C "%CSGO_PATH%\addons\counterstrikesharp\plugins"
del "%DOWNLOAD_DIR%\%zipFile%"

echo Done!
