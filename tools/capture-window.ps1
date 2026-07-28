# Pořídí snímek okna aplikace pro dokumentaci a web.
#
# Použití:
#   .\capture-window.ps1 -Exe <cesta k exe> -Out <cesta k png> [-WaitSeconds 9]
#
# Používá PrintWindow s PW_RENDERFULLCONTENT, ne CopyFromScreen. To je podstatné
# ze dvou důvodů:
#   1) Vykreslí cílové okno i když je překryté nebo není v popředí. Aplikace
#      spuštěná z pozadí se navíc do popředí dostat nemusí - Windows to blokují.
#   2) Nesáhne na zbytek plochy, takže se do snímku nedostane nic jiného, co má
#      uživatel zrovna otevřené.
#
# V TODO.md bylo dřív několikrát zapsáno, že snímky v tomto prostředí nejdou
# pořídit. Nešlo o prostředí, ale o metodu - s PrintWindow fungují.

param([string]$Exe, [string]$Out, [int]$WaitSeconds = 9)
Add-Type -AssemblyName System.Drawing
$sig = @"
using System;
using System.Runtime.InteropServices;
public static class PW {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RC r);
  [StructLayout(LayoutKind.Sequential)] public struct RC { public int L, T, R, B; }
}
"@
Add-Type -TypeDefinition $sig
$p = Start-Process $Exe -PassThru
Start-Sleep -Seconds $WaitSeconds
$p.Refresh()
$h = $p.MainWindowHandle
if ($h -eq [IntPtr]::Zero) { "ZADNE OKNO"; if (-not $p.HasExited) { $p.Kill() }; exit 1 }
$r = New-Object PW+RC
[PW]::GetWindowRect($h, [ref]$r) | Out-Null
$w = $r.R - $r.L; $ht = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$dc = $g.GetHdc()
$ok = [PW]::PrintWindow($h, $dc, 2)
$g.ReleaseHdc($dc)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$colors = @{}
for ($x=0; $x -lt $bmp.Width; $x+=25) { for ($y=0; $y -lt $bmp.Height; $y+=25) { $colors[$bmp.GetPixel($x,$y).ToArgb()] = 1 } }
"PrintWindow=$ok  ${w}x${ht}  $([math]::Round((Get-Item $Out).Length/1KB,1)) KB  barev: $($colors.Count)"
$g.Dispose(); $bmp.Dispose()
if (-not $p.HasExited) { $p.Kill() }
