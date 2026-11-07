# Side-by-side check: starts the WPF build and the Avalonia build (each from a clean temp copy), captures each window, and writes
# <out>\<shot>_wpf.png, <shot>_port.png, <shot>_both.png (WPF left, port right) and <shot>_diff.png (red = differing pixels), then prints the % that differ.
# Shots: "start" (the start screen) and, with -Save, "tracker" (the save is listed as the only recent load and its slot is clicked).
# The % is only a guide (the two text renderers never match pixel for pixel): look at <shot>_both.png.
# Usage: powershell -File tests\side-by-side.ps1 [-Out <folder>] [-Save <save .json>] [-Settle <seconds>] [-RecentY <screen y of the first recent-save slot>]
param(
    [string]$Out = (Join-Path $env:TEMP ("zt_sbs_" + (Get-Date -Format 'yyyyMMdd_HHmmss'))),   # (left in place; delete it yourself)
    [string]$Save = "",
    [int]$Settle = 6,
    [int]$RecentY = 825
)
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing @"
using System; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
public static class Win {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hgt, bool repaint);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  public static void KeepOnTop(IntPtr h) { SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, 3); }   // (HWND_TOPMOST, no move, no resize: other windows must not cover the capture)
  delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
  // the biggest visible top-level window of the process that is not a console window (the WPF build also opens a console)
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
  [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
  public static Bitmap Capture(IntPtr h) {
    RECT r; GetWindowRect(h, out r);
    var b = new Bitmap(r.R - r.L, r.B - r.T, PixelFormat.Format32bppArgb);
    // PrintWindow (PW_RENDERFULLCONTENT) asks the window itself to draw, so another window on top (the emulator, a chat popup) cannot get into the shot
    using (var g = Graphics.FromImage(b)) {
      IntPtr dc = g.GetHdc();
      bool ok = PrintWindow(h, dc, 2);
      g.ReleaseHdc(dc);
      if (!ok) g.CopyFromScreen(r.L, r.T, 0, 0, b.Size);
    }
    return b;
  }
  // returns the fraction of pixels that differ (over the overlap), and writes the diff image
  public static double Diff(Bitmap a, Bitmap b, string diffPath) {
    int w = Math.Min(a.Width, b.Width), h = Math.Min(a.Height, b.Height);
    var d = new Bitmap(w, h); long bad = 0;
    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
      Color p = a.GetPixel(x, y);
      // a pixel counts as the same if the other image has a close match within 1px: text anti-aliasing and 1px glyph shifts then don't count
      bool match = false;
      for (int dy = -1; dy <= 1 && !match; dy++) for (int dx = -1; dx <= 1 && !match; dx++) {
        int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= b.Width || yy >= b.Height) continue;
        Color q = b.GetPixel(xx, yy);
        if (Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B) <= 60) match = true;
      }
      if (!match) { bad++; d.SetPixel(x, y, Color.Red); }
      else d.SetPixel(x, y, Color.FromArgb(40, p.R, p.G, p.B));
    }
    d.Save(diffPath, ImageFormat.Png); d.Dispose();
    return (double)bad / ((double)w * h);
  }
}
public static class Mouse {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  public static void Click(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(150); mouse_event(2, 0, 0, 0, IntPtr.Zero); System.Threading.Thread.Sleep(80); mouse_event(4, 0, 0, 0, IntPtr.Zero); }
}
"@
[void][Win]::SetProcessDPIAware()
New-Item -ItemType Directory -Force $Out | Out-Null
$root = Join-Path $PSScriptRoot ".."
$apps = @(
    @{ Tag = "wpf";  Src = Join-Path $root "Z1R_WPF\bin\Release";                         Exe = "Z1R_WPF.exe" },
    @{ Tag = "port"; Src = Join-Path $root "Z1R_Avalonia_Desktop\bin\Release\net10.0";    Exe = "ZoraTracker.CrossPlatform.exe" }
)
# Launches an app from a clean copy (with the save, when given, as its only recent load) and finds its window
function Start-App($a, $save, $label) {
    $dir = Join-Path $Out ("run_" + $a.Tag + "_" + $label)
    Copy-Item $a.Src $dir -Recurse
    foreach ($f in "Z1R_Tracker_settings.json", "Z1R_Tracker_recent_loads.txt", "Z1R_Tracker_crash_log.txt") { $old = Join-Path $dir $f; if (Test-Path $old) { Remove-Item $old -Force } }
    if ($save) { Set-Content (Join-Path $dir "Z1R_Tracker_recent_loads.txt") $save }
    $p = Start-Process (Join-Path $dir $a.Exe) -WorkingDirectory $dir -PassThru
    $h = [IntPtr]::Zero
    for ($i = 0; $i -lt 60 -and $h -eq [IntPtr]::Zero; $i++) { Start-Sleep -Milliseconds 500; $h = [Win]::FindMain([uint32]$p.Id) }
    if ($h -eq [IntPtr]::Zero) { Write-Host "FAIL $($a.Tag): no window"; try { $p.Kill() } catch {}; return $null }
    Start-Sleep -Seconds $Settle
    return @{ Proc = $p; Hwnd = $h }
}
function Bring-ToFront($app) {
    $r = New-Object Win+RECT; [void][Win]::GetWindowRect($app.Hwnd, [ref]$r)
    [void][Win]::MoveWindow($app.Hwnd, 0, 0, $r.R - $r.L, $r.B - $r.T, $true)   # (top-left corner, size unchanged)
    [Win]::KeepOnTop($app.Hwnd); [void][Win]::SetForegroundWindow($app.Hwnd); Start-Sleep -Milliseconds 800
}
function Snap($app, $name, $tag) {
    Bring-ToFront $app
    [void][Mouse]::SetCursorPos(900, 500); Start-Sleep -Milliseconds 700   # (pointer outside the 784px-wide window: no button is left in its hover look)
    $bmp = [Win]::Capture($app.Hwnd)
    $bmp.Save((Join-Path $Out ($name + "_" + $tag + ".png")), [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host ("{0} {1}: window {2}x{3}" -f $name, $tag, $bmp.Width, $bmp.Height)
    return $bmp
}
function Compare-Shots($name, $w, $q) {
    $both = New-Object System.Drawing.Bitmap ($w.Width + $q.Width + 10), ([Math]::Max($w.Height, $q.Height))
    $g = [System.Drawing.Graphics]::FromImage($both); $g.Clear([System.Drawing.Color]::Gray)
    $g.DrawImage($w, 0, 0); $g.DrawImage($q, $w.Width + 10, 0); $g.Dispose()
    $both.Save((Join-Path $Out ($name + "_both.png")), [System.Drawing.Imaging.ImageFormat]::Png)
    $frac = [Win]::Diff($w, $q, (Join-Path $Out ($name + "_diff.png")))
    Write-Host ("{0}: {1:P2} of pixels differ (sizes {2}x{3} vs {4}x{5})" -f $name, $frac, $w.Width, $w.Height, $q.Width, $q.Height)
}
$start = @{}; $track = @{}
foreach ($a in $apps) {
    $app = Start-App $a $null "start"
    if ($app) { $start[$a.Tag] = Snap $app "start" $a.Tag; try { $app.Proc.Kill() } catch {} }
    if ($Save) {
        $app = Start-App $a (Resolve-Path $Save).Path "tracker"
        if ($app) {
            Bring-ToFront $app
            [Mouse]::Click(150, $RecentY)   # (the first recent-save slot, in the start screen's recents card)
            Start-Sleep -Seconds ($Settle + 8)
            $h2 = [Win]::FindMain([uint32]$app.Proc.Id); if ($h2 -ne [IntPtr]::Zero) { $app.Hwnd = $h2 }
            [Mouse]::Click(400, 930); Start-Sleep -Seconds 1   # (an empty spot on the timeline: the pointer must not hover a tile or tab, the tracker draws hover previews there)
            $track[$a.Tag] = Snap $app "tracker" $a.Tag; try { $app.Proc.Kill() } catch {}
        }
    }
}
if ($start.Count -eq 2) { Compare-Shots "start" $start["wpf"] $start["port"] }
if ($track.Count -eq 2) { Compare-Shots "tracker" $track["wpf"] $track["port"] }
Write-Host "output: $Out"
