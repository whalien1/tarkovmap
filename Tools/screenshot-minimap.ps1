param([string]$ExePath, [string]$OutPath, [string]$InjectFile, [string]$DestName)
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$proc = Start-Process $ExePath -PassThru
Start-Sleep 5
$dest = "C:\Users\whalien\Documents\Escape from Tarkov\Screenshots\$DestName"
if ($InjectFile) {
    Copy-Item -LiteralPath $InjectFile -Destination $dest
    Start-Sleep 3
}
Add-Type -MemberDefinition '[System.Runtime.InteropServices.DllImport("gdi32.dll")] public static extern int GetDeviceCaps(System.IntPtr h, int i);' -Name Gdi -Namespace W
$dc = [System.Drawing.Graphics]::FromHwnd([IntPtr]::Zero).GetHdc()
$w0 = [W.Gdi]::GetDeviceCaps($dc, 118); $h0 = [W.Gdi]::GetDeviceCaps($dc, 117)
$bmp = [System.Drawing.Bitmap]::new($w0, $h0)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(0, 0, 0, 0, [System.Drawing.Size]::new($w0, $h0))
$bmp.Save($OutPath)
$g.Dispose(); $bmp.Dispose()
Remove-Item -LiteralPath $dest -ErrorAction SilentlyContinue
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Output "saved"
