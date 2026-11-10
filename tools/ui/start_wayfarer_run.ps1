param(
    [Parameter(Mandatory=$true)][string]$Run,
    [int]$ProbeHz = 20,
    [int]$Fps = 0,
    [switch]$ReuseBuild,
    [int]$WorldTimeoutSec = 60
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $PSScriptRoot 'WinUi.ps1')
$launcher = Join-Path $root 'tools/launch_mech_test.ps1'
$launchArgs = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$launcher,'-Run',$Run,'-Visible','-ProbeHz',"$ProbeHz",'-Fps',"$Fps")
if ($ReuseBuild) { $launchArgs += '-NoDllCopy' }
& powershell @launchArgs | Out-Host
$runDir = Join-Path $root "runs/$Run"
$gamePid = [int](Get-Content (Join-Path $runDir 'pid.txt'))
$deadline = (Get-Date).AddSeconds(30)
$w = [IntPtr]::Zero
while ((Get-Date) -lt $deadline) {
    $w = Get-CasualtiesWindow -ProcessId $gamePid
    if ($w -ne [IntPtr]::Zero) { break }
    Start-Sleep -Milliseconds 500
}
if ($w -eq [IntPtr]::Zero) { throw 'Game window not found' }
Focus-Window -Handle $w
Start-Sleep -Seconds 3
# Content warning -> title -> run settings -> easy preset -> character -> start.
Click-MouseClient -Handle $w -X 640 -Y 360
Start-Sleep -Seconds 2
Click-MouseClient -Handle $w -X 420 -Y 330
Start-Sleep -Seconds 2
Click-MouseClient -Handle $w -X 590 -Y 300
Start-Sleep -Milliseconds 500
Click-MouseClient -Handle $w -X 480 -Y 690
Start-Sleep -Seconds 2
Click-MouseClient -Handle $w -X 330 -Y 690
$deadline = (Get-Date).AddSeconds($WorldTimeoutSec)
$statusPath = Join-Path $runDir 'status.json'
while ((Get-Date) -lt $deadline) {
    if (Test-Path -LiteralPath $statusPath) {
        try {
            $s = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
            if ($s.ready -and $s.player -and $s.player.beaconCount -ge 1) { break }
        } catch { }
    }
    Start-Sleep -Milliseconds 500
}
Focus-Window -Handle $w
Start-Sleep -Milliseconds 500
Save-WindowScreenshot -Handle $w -Path (Join-Path $runDir 'shot-world-ready.png')
Write-Output "run=$runDir pid=$gamePid window=$w"
