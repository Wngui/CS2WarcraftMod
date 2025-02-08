# Paths (Change these as needed)
$STEAMCMD_PATH = "C:\cs2-server\steamcmd.exe"
$BASE_PATH = "C:\cs2-server\cs2-ds"

$CSGO_PATH = "$BASE_PATH\game\csgo"
$CFG_PATH = "$CSGO_PATH\cfg"
$DOWNLOAD_DIR = "$BASE_PATH\downloads"

# Specific file paths
$SERVER_CFG_PATH = "$CFG_PATH\server.cfg"
$SERVER_BACKUP_PATH = "$CFG_PATH\server_backup.cfg"
$GAMEINFO_FILE = "$CSGO_PATH\gameinfo.gi"

# Backup server.cfg
Write-Host "Backing up server.cfg..."
Copy-Item -Path "$SERVER_CFG_PATH" -Destination "$SERVER_BACKUP_PATH" -Force

# Update CS2
Write-Host "Updating CS2..."
# & "$STEAMCMD_PATH" +login anonymous +force_install_dir "$BASE_PATH" +app_update 730 validate +quit

# Re-add gameinfo entries before lines containing "Game csgo "
Write-Host "Updating gameinfo.gi..."
(Get-Content $GAMEINFO_FILE) -replace "(Game_LowViolence.*)", "`$1`r`n`t`t`tGame`tcsgo/addons/metamod" | Set-Content $GAMEINFO_FILE

Pause

# Create download folder if it does not exist
if (!(Test-Path -Path "$DOWNLOAD_DIR")) {
    New-Item -ItemType Directory -Path "$DOWNLOAD_DIR"
}

# Update Metamod
Write-Host "Updating Metamod..."
$latestDownload = Invoke-RestMethod -Uri "https://mms.alliedmods.net/mmsdrop/2.0/mmsource-latest-windows"
$metamodDownloadPath = "$DOWNLOAD_DIR\$latestDownload"
Invoke-WebRequest -Uri "https://mms.alliedmods.net/mmsdrop/2.0/$latestDownload" -OutFile $metamodDownloadPath
Write-Host "Installing Metamod version $latestDownload..."
tar -xf $metamodDownloadPath -C $CSGO_PATH
Remove-Item -Path $metamodDownloadPath -Force

# Update CounterStrikeSharp
Write-Host "Updating CounterStrikeSharp..."
$latestDownload = (Invoke-RestMethod -Uri "https://api.github.com/repos/roflmuffin/CounterStrikeSharp/releases/latest").assets | Where-Object { $_.browser_download_url -match "with-runtime-build-.*-windows" } | Select-Object -ExpandProperty browser_download_url
Write-Host "Installing CounterStrikeSharp: $latestDownload"
$zipFile = "$DOWNLOAD_DIR\counterstrikesharp.zip"
Invoke-WebRequest -Uri $latestDownload -OutFile $zipFile
tar -xf $zipFile -C $CSGO_PATH
Remove-Item -Path $zipFile -Force

# Restore server.cfg
Write-Host "Restoring server.cfg..."
Copy-Item -Path "$SERVER_BACKUP_PATH" -Destination "$SERVER_CFG_PATH" -Force

Write-Host "Done!"
Pause
