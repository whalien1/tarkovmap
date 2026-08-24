$proc = Start-Process 'D:\tarkov map\TarkovMap\bin\Release\net10.0-windows\TarkovMap.exe' -PassThru
Start-Sleep 4
$proc.Refresh()
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WinEnum {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$target = $proc.Id
[WinEnum]::EnumWindows([WinEnum+EnumProc]{
    param($h, $l)
    $pid2 = 0
    [WinEnum]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
    if ($pid2 -eq $target) {
        $sb = New-Object System.Text.StringBuilder 256
        [WinEnum]::GetWindowText($h, $sb, 256) | Out-Null
        $r = New-Object WinEnum+RECT
        [WinEnum]::GetWindowRect($h, [ref]$r) | Out-Null
        Write-Output ("hwnd={0} visible={1} title='{2}' rect={3},{4}-{5},{6}" -f $h, [WinEnum]::IsWindowVisible($h), $sb.ToString(), $r.Left, $r.Top, $r.Right, $r.Bottom)
    }
    return $true
}, [IntPtr]::Zero) | Out-Null
Stop-Process -Id $proc.Id -Force
