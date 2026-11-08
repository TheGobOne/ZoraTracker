# Action parity check: starts the WPF build and the Avalonia build (each from a clean temp copy), starts a 1st Quest tracker, performs the same scripted
# mouse clicks in each, clicks Save, and compares the two save files field by field (Timeline, OverworldSpotsRemainingOverTime and Blockers are ignored:
# they hold timestamps). A difference means the same clicks left the two trackers in different states.
# The clicks are screen positions of the 784x1006 window moved to the top-left corner; the two builds lay out the same, so one script drives both.
# Usage: powershell -File tests\action-parity.ps1 [-Out <folder>] [-Settle <seconds>]
param(
    [string]$Out = (Join-Path $env:TEMP ("zt_act_" + (Get-Date -Format 'yyyyMMdd_HHmmss'))),   # (left in place; delete it yourself)
    [int]$Settle = 6,
    [string]$Probe = ""   # "x,y,L|R": instead of the scripted run, start a tracker in each build, do that one click, and save <Out>\probe_<build>.png (to see what a click does)
)
Add-Type -ReferencedAssemblies System.Drawing @"
using System; using System.Runtime.InteropServices;
public static class W {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hgt, bool repaint);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  public static void KeepOnTop(IntPtr h) { SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, 3); }
  delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
  public static IntPtr FindMain(uint pid) {
    IntPtr best = IntPtr.Zero; long bestArea = 0;
    EnumWindows((h, l) => {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p != pid || !IsWindowVisible(h)) return true;
      var sb = new System.Text.StringBuilder(256); GetClassName(h, sb, 256);
      string c = sb.ToString(); if (c.Contains("Console") || c.Contains("CASCADIA")) return true;
      RECT r; GetWindowRect(h, out r); long a = (long)(r.R - r.L) * (r.B - r.T);
      if (a > bestArea && (r.R - r.L) >= 300 && (r.B - r.T) >= 300) { bestArea = a; best = h; }
      return true; }, IntPtr.Zero);
    return best;
  }
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
  public static void Key(byte vk) { keybd_event(vk, 0, 0, IntPtr.Zero); System.Threading.Thread.Sleep(60); keybd_event(vk, 0, 2, IntPtr.Zero); System.Threading.Thread.Sleep(400); }
  public static void Click(int x, int y, bool right) {
    SetCursorPos(x, y); System.Threading.Thread.Sleep(120);
    mouse_event(right ? 8u : 2u, 0, 0, 0, IntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(right ? 16u : 4u, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(350);
  }
}
"@
[void][W]::SetProcessDPIAware()
Add-Type -Path (Join-Path $PSScriptRoot "..\Z1R_WPF\bin\Release\Newtonsoft.Json.dll")
New-Item -ItemType Directory -Force $Out | Out-Null
$root = Join-Path $PSScriptRoot ".."
$apps = @(
    @{ Tag = "wpf";  Src = Join-Path $root "Z1R_WPF\bin\Release";                         Exe = "Z1R_WPF.exe" },
    @{ Tag = "port"; Src = Join-Path $root "Z1R_Avalonia_Desktop\bin\Release\net10.0";    Exe = "ZoraTracker.CrossPlatform.exe" }
)
# (x, y, button) in screen pixels; each is a click the tracker handles without opening a popup that would need a second step, or a click that opens one and the next click closes it
$steps = @(
    @(157, 132, 'L'),                  # start screen: 1st Quest (starts the tracker)
    @('wait', 8),
    @(310, 167, 'L'), @(335, 167, 'L'), @(335, 167, 'L'),            # heart containers: on, on, off
    @(303, 135, 'L'), @(303, 135, 'L'), @(333, 135, 'L'),             # item boxes in the blue row
    @(294, 46, 'L'),                                                    # Zones checkbox
    @(110, 520, 'L'), @(190, 520, 'L'), @(387, 520, 'L'),             # dungeon tabs 3, 5 and back to the summary
    @(50, 90, 'L'), @(80, 90, 'L'), @(80, 90, 'L'),                   # triforce / dungeon item boxes in the red grid
    @(303, 135, 'R'), @(80, 90, 'R'), @(50, 90, 'R'),                 # the same boxes, right-clicked
    @(300, 330, 'R'), @('wait', 1), @(187, 373, 'L'), @('wait', 1),   # an overworld tile: pick the yellow "1" in its icon menu
    @(500, 410, 'R'), @('wait', 1), @(400, 930, 'L'), @('wait', 1),   # another tile: its menu opens, a click on the timeline area dismisses it
    @(500, 410, 'R'), @('wait', 1), @(400, 930, 'L'), @('wait', 1),
    @(590, 118, 'L'),                                                   # Log
    @('wait', 2),
    @(590, 118, 'L'),
    @(716, 154, 'L'),                  # Save (asks for a name)
    @('wait', 1),
    @('key', 13),                      # Enter: take the default name
    @('wait', 4)
)
if ($Probe) {
    $pp = $Probe.Split(',')
    if ($pp[0] -eq 'early') { $steps = @( @(157, 132, 'L'), @('wait', 1) ) }   # "early": the shot is taken right after 1st Quest is clicked, while "Loading UI..." shows
    else { $steps = @( @(157, 132, 'L'), @('wait', 8), @([int]$pp[0], [int]$pp[1], $pp[2]), @('wait', 2) ) }
}
Add-Type -AssemblyName System.Drawing
$files = @{}
foreach ($a in $apps) {
    $dir = Join-Path $Out ("run_" + $a.Tag)
    Copy-Item $a.Src $dir -Recurse
    foreach ($f in "Z1R_Tracker_settings.json", "Z1R_Tracker_recent_loads.txt", "Z1R_Tracker_crash_log.txt") { $old = Join-Path $dir $f; if (Test-Path $old) { Remove-Item $old -Force } }
    $savesDir = Join-Path $dir "Saves"
    $before = @(); if (Test-Path $savesDir) { $before = @(Get-ChildItem $savesDir -Filter *.json | ForEach-Object { $_.FullName }) }
    $p = Start-Process (Join-Path $dir $a.Exe) -WorkingDirectory $dir -PassThru
    $h = [IntPtr]::Zero
    for ($i = 0; $i -lt 60 -and $h -eq [IntPtr]::Zero; $i++) { Start-Sleep -Milliseconds 500; $h = [W]::FindMain([uint32]$p.Id) }
    if ($h -eq [IntPtr]::Zero) { Write-Host "FAIL $($a.Tag): no window"; try { $p.Kill() } catch {}; continue }
    Start-Sleep -Seconds $Settle
    $r = New-Object W+RECT; [void][W]::GetWindowRect($h, [ref]$r)
    [void][W]::MoveWindow($h, 0, 0, $r.R - $r.L, $r.B - $r.T, $true); [W]::KeepOnTop($h); [void][W]::SetForegroundWindow($h); Start-Sleep -Milliseconds 800
    foreach ($s in $steps) {
        if ($s[0] -eq 'wait') { Start-Sleep -Seconds $s[1]; continue }
        if ($s[0] -eq 'key') { [W]::Key([byte]$s[1]); continue }
        [W]::Click([int]$s[0], [int]$s[1], ($s[2] -eq 'R'))
    }
    if ($Probe) {
        $rr = New-Object W+RECT; [void][W]::GetWindowRect($h, [ref]$rr)
        $bmp = New-Object System.Drawing.Bitmap ($rr.R - $rr.L), ($rr.B - $rr.T)
        $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($rr.L, $rr.T, 0, 0, $bmp.Size); $g.Dispose()
        $bmp.Save((Join-Path $Out ("probe_" + $a.Tag + ".png")), [System.Drawing.Imaging.ImageFormat]::Png)
        try { $p.Kill() } catch {}; continue
    }
    try { $p.Kill() } catch {}
    $new = @(); if (Test-Path $savesDir) { $new = @(Get-ChildItem $savesDir -Filter *.json | Where-Object { $before -notcontains $_.FullName } | Sort-Object LastWriteTime) }
    if ($new.Count -eq 0) { Write-Host "FAIL $($a.Tag): no save file was written"; continue }
    $files[$a.Tag] = $new[-1].FullName
    Write-Host ("{0}: {1} ({2} bytes)" -f $a.Tag, $new[-1].Name, $new[-1].Length)
}
if ($files.Count -lt 2) { Write-Host "cannot compare"; exit 1 }
$ignore = @('Timeline', 'OverworldSpotsRemainingOverTime', 'Blockers', 'TimeInSeconds')   # (the timer: the two runs take slightly different times)
function Compare-Json($a, $b, $path, $diffs) {
    if ($null -eq $a -and $null -eq $b) { return }
    if ($a -is [Newtonsoft.Json.Linq.JObject] -and $b -is [Newtonsoft.Json.Linq.JObject]) {
        $names = @($a.Properties() | ForEach-Object { $_.Name }) + @($b.Properties() | ForEach-Object { $_.Name }) | Select-Object -Unique
        foreach ($n in $names) {
            if ($ignore -contains $n) { continue }
            $x = $a[$n]; $y = $b[$n]
            if ($null -eq $x) { [void]$diffs.Add("$path.$n : only in the port's save"); continue }
            if ($null -eq $y) { [void]$diffs.Add("$path.$n : only in WPF's save"); continue }
            Compare-Json $x $y "$path.$n" $diffs
        }
    } elseif ($a -is [Newtonsoft.Json.Linq.JArray] -and $b -is [Newtonsoft.Json.Linq.JArray]) {
        if ($a.Count -ne $b.Count) { [void]$diffs.Add("$path : array length $($a.Count) vs $($b.Count)"); return }
        for ($i = 0; $i -lt $a.Count; $i++) { Compare-Json $a[$i] $b[$i] "$path[$i]" $diffs }
    } else {
        if ($a.ToString() -ne $b.ToString()) { [void]$diffs.Add("$path : WPF '$a' vs port '$b'") }
    }
}
$wj = [Newtonsoft.Json.Linq.JObject]::Parse((Get-Content $files["wpf"] -Raw))
$pj = [Newtonsoft.Json.Linq.JObject]::Parse((Get-Content $files["port"] -Raw))
$diffs = New-Object System.Collections.ArrayList
Compare-Json $wj $pj "" $diffs
$real = @($diffs | Where-Object { $_ -notlike "*only in the port's save" -and $_ -notlike "*only in WPF's save" })
$extra = @($diffs | Where-Object { $_ -like "*only in*" })
Write-Host ("{0} difference(s), {1} field(s) present in only one save" -f $real.Count, $extra.Count)
$real | Select-Object -First 25 | ForEach-Object { Write-Host "    $_" }
$extra | Select-Object -First 10 | ForEach-Object { Write-Host "    (one-sided) $_" }
# Round trip: the port's save is loaded by the WPF build (listed as its only recent load, its slot clicked), WPF saves it again, and that save is compared with the port's
# (what the port wrote must survive WPF loading it: switching back to WPF must not lose anything)
$rtDir = Join-Path $Out "run_wpf_roundtrip"
Copy-Item $apps[0].Src $rtDir -Recurse
foreach ($f in "Z1R_Tracker_settings.json", "Z1R_Tracker_recent_loads.txt", "Z1R_Tracker_crash_log.txt") { $old = Join-Path $rtDir $f; if (Test-Path $old) { Remove-Item $old -Force } }
Set-Content (Join-Path $rtDir "Z1R_Tracker_recent_loads.txt") $files["port"]
$rtBefore = @(Get-ChildItem (Join-Path $rtDir "Saves") -Filter *.json | ForEach-Object { $_.FullName })
$p = Start-Process (Join-Path $rtDir "Z1R_WPF.exe") -WorkingDirectory $rtDir -PassThru
$h = [IntPtr]::Zero
for ($i = 0; $i -lt 60 -and $h -eq [IntPtr]::Zero; $i++) { Start-Sleep -Milliseconds 500; $h = [W]::FindMain([uint32]$p.Id) }
Start-Sleep -Seconds $Settle
$r = New-Object W+RECT; [void][W]::GetWindowRect($h, [ref]$r)
[void][W]::MoveWindow($h, 0, 0, $r.R - $r.L, $r.B - $r.T, $true); [W]::KeepOnTop($h); [void][W]::SetForegroundWindow($h); Start-Sleep -Milliseconds 800
[W]::Click(150, 825, $false); Start-Sleep -Seconds ($Settle + 8)   # (the first recent-save slot)
[W]::Click(716, 154, $false); Start-Sleep -Seconds 1; [W]::Key(13); Start-Sleep -Seconds 4   # Save, default name
try { $p.Kill() } catch {}
$rt = @(Get-ChildItem (Join-Path $rtDir "Saves") -Filter *.json | Where-Object { $rtBefore -notcontains $_.FullName } | Sort-Object LastWriteTime)
$rtFail = $true
if ($rt.Count -eq 0) { Write-Host "ROUND TRIP FAIL: WPF wrote no save after loading the port's" }
else {
    $rj = [Newtonsoft.Json.Linq.JObject]::Parse((Get-Content $rt[-1].FullName -Raw))
    $rd = New-Object System.Collections.ArrayList
    Compare-Json $pj $rj "" $rd
    $rreal = @($rd | Where-Object { $_ -notlike "*only in*" })
    Write-Host ("round trip (port save -> WPF load -> WPF save): {0} difference(s), {1} one-sided field(s)" -f $rreal.Count, @($rd | Where-Object { $_ -like "*only in*" }).Count)
    $rreal | Select-Object -First 25 | ForEach-Object { Write-Host "    $_" }
    $rd | Where-Object { $_ -like "*only in*" } | Select-Object -First 10 | ForEach-Object { Write-Host "    (one-sided) $_" }
    $rtFail = ($rreal.Count -gt 0)
}
Write-Host "output: $Out"
exit $(if ($real.Count -eq 0 -and -not $rtFail) { 0 } else { 1 })
