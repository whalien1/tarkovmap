param([string]$ExePath, [string]$OutPrefix, [string]$InjectFile, [string]$DestName)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class WinCap4 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
Get-Process TarkovMap -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 1
$proc = Start-Process $ExePath -PassThru
Start-Sleep 5
if ($InjectFile) {
    $dest = "C:\Users\whalien\Documents\Escape from Tarkov\Screenshots\$DestName"
    Copy-Item -LiteralPath $InjectFile -Destination $dest
    Start-Sleep 3
}
$proc.Refresh()
$target = $proc.Id
$wins = New-Object System.Collections.Generic.List[IntPtr]
$cb = [WinCap4+EnumProc]{
    param($h, $l)
    $pid2 = 0
    [WinCap4]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
    if ($pid2 -eq $target -and [WinCap4]::IsWindowVisible($h)) { $wins.Add($h) }
    return $true
}
[WinCap4]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
Write-Output "windows: $($wins.Count)"
$i = 0
foreach ($h in $wins) {
    $r = New-Object WinCap4+RECT
    [WinCap4]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $hh = $r.Bottom - $r.Top
    Write-Output ("win $i : ${w}x${hh} at $($r.Left),$($r.Top)")
    if ($w -gt 10 -and $hh -gt 10) {
        $bmp = [System.Drawing.Bitmap]::new($w, $hh)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $hdc = $g.GetHdc()
        [WinCap4]::PrintWindow($h, $hdc, 2) | Out-Null
        $g.ReleaseHdc($hdc)
        $bmp.Save("$OutPrefix-$i.png")
        $g.Dispose(); $bmp.Dispose()
    }
    $i++
}
Remove-Item -LiteralPath $dest -ErrorAction SilentlyContinue
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Output "saved"
