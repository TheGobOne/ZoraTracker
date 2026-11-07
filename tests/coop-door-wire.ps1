# Co-op wire check: the DoorChange JSON that the WPF build and the Avalonia build send must be identical for every door state, and each build must read the other's.
# (Not a live co-op session: that needs a host and two trackers; this checks the one place the two builds' types differ, see PROGRESS.md "CoopSync wire format".)
# Usage: powershell -File tests\coop-door-wire.ps1      (needs both Release builds)
$wpf = Join-Path $PSScriptRoot "..\Z1R_WPF\bin\Release"
[void][Reflection.Assembly]::LoadFrom("$wpf\Newtonsoft.Json.dll"); [void][Reflection.Assembly]::LoadFrom("$wpf\Z1R_Tracker.dll"); [void][Reflection.Assembly]::LoadFrom("$wpf\Z1R_Sync.dll")
$asm = [Reflection.Assembly]::LoadFrom("$wpf\Z1R_WPF.exe")
$ds = $asm.GetType("Dungeon+DoorState"); $dc = $asm.GetType("DungeonUI+DoorChangeInfo")
$wpfLines = @()
foreach ($n in "UNKNOWN", "NO", "YES", "YELLOW", "PURPLE") {
    $o = [Activator]::CreateInstance($dc, @(3, 5, 2, $true, $ds.GetProperty($n).GetValue($null)))
    $wpfLines += [Newtonsoft.Json.JsonConvert]::SerializeObject($o)
}
$file = Join-Path $env:TEMP "zt_wpf_doors.txt"
Set-Content $file $wpfLines
$portOut = @(dotnet run -c Release --project (Join-Path $PSScriptRoot "coop-door-wire\coop-door-wire.csproj") -- $file 2>&1 | Where-Object { $_ -like '{*' -or $_ -like 'READ *' })
$portLines = @($portOut | Where-Object { $_ -like '{*' })
$reads = @($portOut | Where-Object { $_ -like 'READ *' })
$bad = 0
for ($i = 0; $i -lt 5; $i++) { if ($wpfLines[$i] -ne $portLines[$i]) { $bad++; Write-Host "DIFF: WPF $($wpfLines[$i])  port $($portLines[$i])" } }
# (the port reading WPF's lines: each READ line must show the same state before and after the arrow)
foreach ($r in $reads) { if ($r -notmatch '^READ (\w+) -> \1 ') { $bad++; Write-Host "port misread: $r" } }
if ($reads.Count -ne 5) { $bad++; Write-Host "the port read $($reads.Count) of 5 lines" }
# (WPF reading the port's lines)
foreach ($l in $portLines) { $x = [Newtonsoft.Json.JsonConvert]::DeserializeObject($l, $dc); if (($l -notmatch ('"Case":"' + $x.NewState.ToString() + '"'))) { $bad++; Write-Host "WPF misread: $l" } }
if ($bad -eq 0) { Write-Host "PASS: DoorChange JSON is identical for all 5 door states and each build reads the other's" } else { Write-Host "FAIL ($bad)" }
exit $(if ($bad -eq 0) { 0 } else { 1 })
