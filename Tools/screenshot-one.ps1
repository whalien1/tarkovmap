param([string]$ExePath, [string]$OutPath, [string]$InjectFile, [string]$DestName)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Cap3 {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@
$proc = Start-Process $ExePath -PassThru
Start-Sleep 4
$proc.Refresh()
$tries = 0
while ($proc.MainWindowHandle -eq [IntPtr]::Zero -and $tries -lt 15) {
    Start-Sleep 1
    $proc.Refresh()
    $tries++
}
$dest = "C:\Users\whalien\Documents\Escape from Tarkov\Screenshots\$DestName"
if ($InjectFile) {
    Copy-Item -LiteralPath $InjectFile -Destination $dest
    Start-Sleep 3
}
$proc.Refresh()
$r = New-Object Win32Cap3+RECT
[Win32Cap3]::GetWindowRect($proc.MainWindowHandle, [ref]$r) | Out-Null
$w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
Write-Output "window ${w}x${h}"
$bmp = [System.Drawing.Bitmap]::new($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[Win32Cap3]::PrintWindow($proc.MainWindowHandle, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc)
$bmp.Save($OutPath)
$g.Dispose(); $bmp.Dispose()
Remove-Item -LiteralPath $dest -ErrorAction SilentlyContinue
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Output "saved"
