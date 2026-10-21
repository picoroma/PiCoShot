<#
  Crea il pacchetto MSIX di PiCoShot a partire da dist\PiCoShot.exe (lancia prima .\build.ps1).

  Per lo Store NON serve firmare: lo Store ri-firma il pacchetto. Per una prova locale (installazione) serve firmarlo:
  vedi -PfxPath. Identity/Publisher reali = quelli di Partner Center (Product management > Product identity).

  Esempi
    .\packaging\msix\build-msix.ps1
    .\packaging\msix\build-msix.ps1 -IdentityName "12345Nome.PiCoShot" -Publisher "CN=AAAA-BBBB-..." -PublisherDisplayName "Il Tuo Nome"
#>
param(
    [string]$IdentityName = 'PiCoShot.Test',
    [string]$Publisher = 'CN=PiCoShot Test',
    [string]$PublisherDisplayName = 'PiCoShot Test',
    [string]$Version,                       # 4 numeri, l'ultimo 0 (riservato allo Store); predefinito: da AssemblyInfo.cs
    [string]$ExePath,
    [string]$OutDir,
    [string]$PfxPath,                       # opzionale: firma per prove locali
    [string]$PfxPassword
)
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $ExePath) { $ExePath = Join-Path $root 'dist\PiCoShot.exe' }
if (-not $OutDir) { $OutDir = Join-Path $root 'dist' }
if (-not (Test-Path $ExePath)) { throw "Exe non trovato: $ExePath. Lancia prima .\build.ps1" }

# versione da AssemblyInfo.cs (es. 0.2.0.0)
if (-not $Version) {
    $info = Get-Content (Join-Path $root 'src\PiCoShot\Properties\AssemblyInfo.cs') -Raw
    if ($info -notmatch 'AssemblyVersion\("(\d+)\.(\d+)\.(\d+)\.\d+"\)') { throw 'AssemblyVersion non trovata in AssemblyInfo.cs' }
    $Version = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"
}
if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') { throw "Versione non valida '$Version': servono 4 numeri con l'ultimo uguale a 0." }

# strumenti del Windows SDK (versione più recente)
function Find-SdkTool([string]$name) {
    $bin = 'C:\Program Files (x86)\Windows Kits\10\bin'
    $t = Get-ChildItem $bin -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\$name" } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $t) { throw "$name non trovato: installa il Windows 10/11 SDK." }
    return $t
}
$makeappx = Find-SdkTool 'makeappx.exe'

Add-Type -AssemblyName System.Drawing

# cartella di staging
$stage = Join-Path $OutDir 'msix-staging'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'Assets') | Out-Null
Copy-Item $ExePath (Join-Path $stage 'PiCoShot.exe')

# loghi generati dall'icona dell'app (la voce più grande dell'.ico)
$icoPath = Join-Path $root 'src\PiCoShot\Resources\picoshot.ico'
$ico = [IO.File]::ReadAllBytes($icoPath)
$count = [BitConverter]::ToUInt16($ico, 4)
$best = $null
for ($i = 0; $i -lt $count; $i++) {
    $o = 6 + 16 * $i
    $w = [int]$ico[$o]; if ($w -eq 0) { $w = 256 }
    if (-not $best -or $w -gt $best.W) {
        $best = @{ W = $w; Len = [BitConverter]::ToUInt32($ico, $o + 8); Off = [BitConverter]::ToUInt32($ico, $o + 12) }
    }
}
$png = New-Object byte[] $best.Len
[Array]::Copy($ico, $best.Off, $png, 0, $best.Len)
$src = [Drawing.Image]::FromStream((New-Object IO.MemoryStream (, $png)))

function New-Logo([string]$file, [int]$w, [int]$h) {
    $bmp = New-Object Drawing.Bitmap $w, $h, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $s = [Math]::Min($w, $h)
    $g.DrawImage($src, [int](($w - $s) / 2), [int](($h - $s) / 2), $s, $s)
    $g.Dispose()
    $bmp.Save((Join-Path $stage "Assets\$file"), [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
New-Logo 'StoreLogo.png' 50 50
New-Logo 'Square44x44Logo.png' 44 44
New-Logo 'Square71x71Logo.png' 71 71
New-Logo 'Square150x150Logo.png' 150 150
New-Logo 'Square310x310Logo.png' 310 310
New-Logo 'Wide310x150Logo.png' 310 150
$src.Dispose()

# manifest dal modello
$manifest = Get-Content (Join-Path $PSScriptRoot 'AppxManifest.template.xml') -Raw -Encoding UTF8
$manifest = $manifest.Replace('{{IDENTITY_NAME}}', $IdentityName).Replace('{{PUBLISHER}}', $Publisher).
    Replace('{{PUBLISHER_DISPLAY_NAME}}', $PublisherDisplayName).Replace('{{VERSION}}', $Version)
[IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding $false))

# pacchetto
$msix = Join-Path $OutDir "PiCoShot_${Version}_x64.msix"
if (Test-Path $msix) { Remove-Item $msix -Force }
& $makeappx pack /d $stage /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx pack fallito (codice $LASTEXITCODE)" }

if ($PfxPath) {
    $signtool = Find-SdkTool 'signtool.exe'
    $sargs = @('sign', '/fd', 'SHA256', '/f', $PfxPath)
    if ($PfxPassword) { $sargs += @('/p', $PfxPassword) }
    & $signtool @sargs $msix
    if ($LASTEXITCODE -ne 0) { throw "signtool fallito (codice $LASTEXITCODE)" }
}

"OK: $msix  ($([math]::Round((Get-Item $msix).Length / 1KB)) KB)"
"    Identity: $IdentityName | Publisher: $Publisher | Versione: $Version"
if (-not $PfxPath) { "    Non firmato: va bene per l'upload su Partner Center (lo Store ri-firma); per installarlo in locale serve firmarlo." }
