<#
  Compila PiCoShot.exe in dist\ SENZA .NET SDK: usa il compilatore C# di .NET Framework (csc.exe, già in Windows)
  e i metadati WinRT del Windows SDK (per l'OCR). Alternativa con l'SDK: dotnet build src\PiCoShot\PiCoShot.csproj -c Release

  Uso:  .\build.ps1
#>
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$proj = Join-Path $root 'src\PiCoShot'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null

$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe non trovato in $fw" }

# metadati WinRT (Windows.winmd) dal Windows SDK più recente installato
$unionBase = 'C:\Program Files (x86)\Windows Kits\10\UnionMetadata'
$winmd = $null
if (Test-Path $unionBase) {
    # solo le cartelle numerate (10.0.xxxxx.0): la cartella "Facade" contiene un winmd vuoto di tipi
    $dir = Get-ChildItem $unionBase -Directory |
        Where-Object { $_.Name -match '^\d+(\.\d+)+$' -and (Test-Path (Join-Path $_.FullName 'Windows.winmd')) } |
        Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if ($dir) { $winmd = Join-Path $dir.FullName 'Windows.winmd' }
}
if (-not $winmd) { throw "Windows.winmd non trovato: installa il Windows 10/11 SDK (serve per l'OCR)." }

$sources = Get-ChildItem $proj -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } | ForEach-Object { $_.FullName }

$out = Join-Path $dist 'PiCoShot.exe'
$args = @(
    '-nologo', '-target:winexe', '-platform:x64', '-optimize+',
    "-out:$out",
    "-win32manifest:$proj\app.manifest",
    "-win32icon:$proj\Resources\picoshot.ico",
    "-resource:$proj\Resources\about.jpg,PiCoShot.about.jpg",
    '-r:System.dll', '-r:System.Core.dll', '-r:System.Drawing.dll', '-r:System.Windows.Forms.dll',
    "-r:$fw\System.Runtime.dll", "-r:$fw\System.Runtime.WindowsRuntime.dll", "-r:$fw\System.Threading.Tasks.dll",
    "-r:$winmd"
) + $sources

& $csc @args
if ($LASTEXITCODE -ne 0) { throw "Compilazione fallita (codice $LASTEXITCODE)" }

$vi = [Diagnostics.FileVersionInfo]::GetVersionInfo($out)
"OK: $out  ($([math]::Round((Get-Item $out).Length / 1KB)) KB, versione $($vi.FileVersion))"
