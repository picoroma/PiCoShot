<#
  Crea lo ZIP portable di PiCoShot a partire da dist\PiCoShot.exe (lancia prima .\build.ps1).

  Contenuto: una cartella PiCoShot-<versione>\ con PiCoShot.exe, LICENSE (GPL v3) e LEGGIMI.txt (IT/EN).
  Uscita: dist\PiCoShot-portable-x64.zip + dist\PiCoShot-portable-x64.zip.sha256
  (nome fisso: l'indirizzo releases/latest/download/PiCoShot-portable-x64.zip punta sempre all'ultima versione).

  Uso:  .\packaging\zip\build-zip.ps1
#>
param(
    [string]$ExePath,
    [string]$OutDir
)
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $ExePath) { $ExePath = Join-Path $root 'dist\PiCoShot.exe' }
if (-not $OutDir) { $OutDir = Join-Path $root 'dist' }
if (-not (Test-Path $ExePath)) { throw "Exe non trovato: $ExePath. Lancia prima .\build.ps1" }

# versione dall'exe (deve coincidere con AssemblyInfo.cs)
$vi = [Diagnostics.FileVersionInfo]::GetVersionInfo($ExePath)
$ver = "{0}.{1}.{2}" -f $vi.FileMajorPart, $vi.FileMinorPart, $vi.FileBuildPart
$folder = "PiCoShot-$ver"

$files = @(
    @{ Src = $ExePath; Name = 'PiCoShot.exe' },
    @{ Src = (Join-Path $root 'LICENSE'); Name = 'LICENSE' },
    @{ Src = (Join-Path $PSScriptRoot 'README-portable.txt'); Name = 'LEGGIMI.txt' }
)
foreach ($f in $files) { if (-not (Test-Path $f.Src)) { throw "File mancante: $($f.Src)" } }

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zipPath = Join-Path $OutDir 'PiCoShot-portable-x64.zip'
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

$zip = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($f in $files) {
        # nomi con "/" (compatibili con ogni programma di decompressione)
        $entry = $zip.CreateEntry("$folder/$($f.Name)", [IO.Compression.CompressionLevel]::Optimal)
        $in = [IO.File]::OpenRead($f.Src)
        $out = $entry.Open()
        try { $in.CopyTo($out) } finally { $out.Dispose(); $in.Dispose() }
    }
}
finally { $zip.Dispose() }

$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLower()
[IO.File]::WriteAllText("$zipPath.sha256", "$hash  PiCoShot-portable-x64.zip`n", (New-Object Text.UTF8Encoding $false))

"OK: $zipPath  ($([math]::Round((Get-Item $zipPath).Length / 1KB)) KB, versione $ver)"
"    SHA256: $hash"
