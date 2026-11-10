# Background self-test of the tracker: runs the Release build from a COPY (the test changes settings) once per quest type.
# Usage: powershell -File selftest.ps1 [-Quests 0,1,2,3] [-TimeoutMinutes 12]
# (-Quests is text, split below: with `powershell -File`, "-Quests 0,1,2,3" arrives as ONE string, which an [int[]] parameter read as the number 123)
param([string]$Quests = "0,1,2,3", [int]$TimeoutMinutes = 12, [switch]$Port)   # -Port: the cross-platform (Avalonia) build instead of WPF
$questList = @($Quests -split '[,\s]+' | Where-Object { $_ -ne '' } | ForEach-Object { [int]$_ })
$src = Join-Path $PSScriptRoot "Z1R_WPF\bin\Release"
$exeName = "Z1R_WPF.exe"
if ($Port) { $src = Join-Path $PSScriptRoot "Z1R_Avalonia_Desktop\bin\Release\net10.0"; $exeName = "ZoraTracker.CrossPlatform.exe" }
$overall = 0
foreach ($q in $questList) {
    $dir = Join-Path $env:TEMP ($(if ($Port) {"zt_selftest_port_$q"} else {"zt_selftest_$q"}))
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    Copy-Item $src $dir -Recurse
    # (the copy must start clean: remembered settings and the recent-loads list are the developer's own, and the test would click the recent-load buttons and start a game from a save mid-test)
    foreach ($leftover in "Z1R_Tracker_settings.json", "Z1R_Tracker_recent_loads.txt") { $f = Join-Path $dir $leftover; if (Test-Path $f) { Remove-Item $f -Force } }
    $crash = Join-Path $dir "Z1R_Tracker_crash_log.txt"
    if (Test-Path $crash) { Remove-Item $crash -Force }
    $p = Start-Process (Join-Path $dir $exeName) -ArgumentList "--selftest","--quest=$q" -WorkingDirectory $dir -PassThru
    if (-not $p.WaitForExit($TimeoutMinutes * 60 * 1000)) { $p.Kill(); Add-Content (Join-Path $dir "selftest_report.txt") "FAIL: timed out after $TimeoutMinutes minutes (hung)" }
    $report = Join-Path $dir "selftest_report.txt"
    Write-Host "=== quest $q ==="
    if (Test-Path $report) { Get-Content $report | Select-Object -Last 40 } else { Write-Host "no report written (the tracker did not start?)" ; $overall = 1 }
    if (Test-Path $crash) { Write-Host "CRASH LOG:"; Get-Content $crash | Select-Object -First 25; $overall = 1 }
    if ((Test-Path $report) -and -not (Select-String -Path $report -Pattern "RESULT: PASS" -Quiet)) { $overall = 1 }
}
if ($overall -eq 0) { Write-Host "ALL PASS" } else { Write-Host "SOME FAILED" }
exit $overall
