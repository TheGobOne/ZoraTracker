# Golden save check: every save file in a folder (default: the WPF build's Saves folder) is loaded by the Avalonia build (--load/--dump),
# and the tracker's own re-save is compared with the file that was loaded. Anything that differs is a save-compatibility gap
# (a field the port drops, renames or reorders). Some parts always differ and are ignored (see $ignore below; docs/load-dump.md).
# Usage: powershell -File tests\golden-save.ps1 [-SavesDir <folder>] [-Build <Avalonia release folder>]
param(
    [string]$SavesDir = (Join-Path $PSScriptRoot "..\Z1R_WPF\bin\Release\Saves"),
    [string]$Build    = (Join-Path $PSScriptRoot "..\Z1R_Avalonia_Desktop\bin\Release\net10.0")
)
$ignore = @('Timeline', 'OverworldSpotsRemainingOverTime', 'Blockers')
$work = Join-Path $env:TEMP ("zt_golden_" + (Get-Date -Format 'yyyyMMdd_HHmmss'))   # (left in place; delete it yourself)
Copy-Item $Build $work -Recurse
$exe = Join-Path $work "ZoraTracker.CrossPlatform.exe"

# (Windows PowerShell 5.1's ConvertFrom-Json and JavaScriptSerializer reject these saves, so the JSON is read with the Newtonsoft.Json that ships with the WPF build)
Add-Type -Path (Join-Path $PSScriptRoot "..\Z1R_WPF\bin\Release\Newtonsoft.Json.dll")
function Compare-Json($a, $b, $path, $diffs) {
    if ($null -eq $a -and $null -eq $b) { return }
    if ($a -is [Newtonsoft.Json.Linq.JObject] -and $b -is [Newtonsoft.Json.Linq.JObject]) {
        $names = @($a.Properties() | ForEach-Object { $_.Name }) + @($b.Properties() | ForEach-Object { $_.Name }) | Select-Object -Unique
        foreach ($n in $names) {
            if ($ignore -contains $n) { continue }
            $x = $a[$n]; $y = $b[$n]
            if ($null -eq $x) { [void]$diffs.Add("$path.$n : only in the re-save"); continue }
            if ($null -eq $y) { [void]$diffs.Add("$path.$n : missing from the re-save"); continue }
            Compare-Json $x $y "$path.$n" $diffs
        }
    } elseif ($a -is [Newtonsoft.Json.Linq.JArray] -and $b -is [Newtonsoft.Json.Linq.JArray]) {
        if ($a.Count -ne $b.Count) { [void]$diffs.Add("$path : array length $($a.Count) vs $($b.Count)"); return }
        for ($i = 0; $i -lt $a.Count; $i++) { Compare-Json $a[$i] $b[$i] "$path[$i]" $diffs }
    } else {
        if ($a.ToString() -ne $b.ToString()) { [void]$diffs.Add("$path : '$a' vs '$b'") }
    }
}

$failed = 0
foreach ($f in Get-ChildItem $SavesDir -Filter *.json) {
    $out = Join-Path $work ($f.BaseName + ".dump.json")
    $p = Start-Process $exe -ArgumentList "--load=`"$($f.FullName)`"", "--dump=`"$out`"" -WorkingDirectory $work -PassThru -WindowStyle Hidden
    if (-not $p.WaitForExit(120000)) { $p.Kill(); Write-Host "FAIL $($f.Name): timed out"; $failed++; continue }
    if (-not (Test-Path $out)) { Write-Host "FAIL $($f.Name): no dump written"; $failed++; continue }
    $d = [Newtonsoft.Json.Linq.JObject]::Parse((Get-Content $out -Raw))
    if ($d['error'].Type -ne 'Null') { Write-Host "FAIL $($f.Name): load error: $($d['error'])"; $failed++; continue }
    try { $orig = [Newtonsoft.Json.Linq.JObject]::Parse((Get-Content $f.FullName -Raw)) }
    catch { Write-Host "FAIL $($f.Name): the saved file itself is not valid JSON: $($_.Exception.Message)"; $failed++; continue }
    $diffs = New-Object System.Collections.ArrayList
    Compare-Json $orig $d['save'] "" $diffs
    $note = @(); if ($d['warning'].Type -ne 'Null') { $note += "warning: $($d['warning'])" }; if ($d['setProblems'].ToString() -eq 'True') { $note += "an item box refused a value" }
    # (a field that only the re-save has is a newer save format adding something an older file lacks: reported, not a failure)
    $added = @($diffs | Where-Object { $_ -like '*only in the re-save' })
    $real = @($diffs | Where-Object { $_ -notlike '*only in the re-save' })
    if ($real.Count -eq 0 -and $note.Count -eq 0) { Write-Host "PASS $($f.Name)  ($($added.Count) newer field(s) added by the re-save)" }
    else {
        $failed++
        Write-Host "DIFF $($f.Name): $($real.Count) difference(s) $($note -join '; ')"
        $real | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" }
    }
}
if ($failed -eq 0) { Write-Host "ALL PASS" } else { Write-Host "SOME DIFFER ($failed)" }
exit $(if ($failed -eq 0) { 0 } else { 1 })
