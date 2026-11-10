param(
    [Parameter(Mandatory=$true)][string]$Run,
    [switch]$Visible,
    [switch]$SkipProbe,
    [switch]$NoDllCopy,
    [int]$ProbeHz = 20,
    [int]$Fps = 0
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ($Run -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Use a simple run name' }
$game = Join-Path $root '.local/cu-test/game'
$exe = Join-Path $game 'CasualtiesUnknown.exe'
$running = Get-Process CasualtiesUnknown -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }
if ($running) { throw 'Previous test instance is still running (same sandbox exe)' }
$runDir = Join-Path $root "runs/$Run"
if (Test-Path -LiteralPath $runDir) { throw 'Use a new run directory' }
New-Item -ItemType Directory -Path $runDir | Out-Null
if (-not $NoDllCopy) {
    Copy-Item -LiteralPath (Join-Path $root 'src/WayfarerMech/bin/Release/net48/WayfarerMech.dll') -Destination (Join-Path $game 'BepInEx/plugins/WayfarerMech/WayfarerMech.dll') -Force
}
$probePath = Join-Path $game 'BepInEx/plugins/MechProbe.dll'
if ($SkipProbe) {
    if (Test-Path -LiteralPath $probePath) { Remove-Item -LiteralPath $probePath -Force }
} elseif (-not $NoDllCopy) {
    Copy-Item -LiteralPath (Join-Path $root 'tools/MechProbe/bin/Release/net48/MechProbe.dll') -Destination $probePath -Force
}
$env:CU_PROBE_DIR = $runDir
$env:CU_PROBE_HZ = "$ProbeHz"
$env:CU_PROBE_FPS = "$Fps"
$env:CU_TEST_DATA = Join-Path $root '.local/cu-test/userdata'
$logPath = Join-Path $runDir 'Player.log'
$gameArgs = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile `"$logPath`""
$startArgs = @{ FilePath = $exe; WorkingDirectory = $game; ArgumentList = $gameArgs; PassThru = $true }
if (-not $Visible) { $startArgs.WindowStyle = 'Hidden' }
$testProcess = Start-Process @startArgs
$testProcess.Id | Set-Content (Join-Path $runDir 'pid.txt')
Get-FileHash (Join-Path $game 'BepInEx/plugins/WayfarerMech/WayfarerMech.dll') | ConvertTo-Json | Set-Content (Join-Path $runDir 'mod-hash.json')
Write-Output "Test PID $($testProcess.Id); run $runDir"
