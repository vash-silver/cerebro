#requires -Version 5.1

<#
.SYNOPSIS
    Build a self-contained release zip of MarvelHeroes.DpsMeter ready to send to a non-developer.

.DESCRIPTION
    Wraps `dotnet publish` with the exact set of flags that produced the v1.0 release zip:
        - self-contained .NET 8 single-file EXE (recipient does NOT need .NET installed)
        - native libraries embedded; no loose DLLs on disk
        - single-file payload compressed
        - no PDBs, no debug symbols, no source paths in IL
        - deterministic build

    Then copies scripts/PackageReadme.txt next to the EXE and zips both together as
    publish/MarvelHeroesDpsMeter-v<Version>.zip.

    The publish folder is gitignored (see .gitignore), so nothing committed.

.PARAMETER Version
    Version tag baked into the zip filename.  Default: "1.0".
    Example: -Version "1.1" produces publish/MarvelHeroesDpsMeter-v1.1.zip

.PARAMETER SkipLeakScan
    Skip the personal-info scan over the produced binary.  Off by default; the
    scan adds a second or two and is cheap insurance against shipping a build
    that accidentally embedded "C:\Users\<you>\..." paths.

.EXAMPLE
    .\scripts\publish.ps1
    Produces publish/MarvelHeroesDpsMeter-v1.0.zip with default flags.

.EXAMPLE
    .\scripts\publish.ps1 -Version 1.2
    Produces publish/MarvelHeroesDpsMeter-v1.2.zip.
#>

[CmdletBinding()]
param(
    [string] $Version = '1.0',
    [switch] $SkipLeakScan
)

$ErrorActionPreference = 'Stop'

# Paths --------------------------------------------------------------------
# $PSScriptRoot is scripts/, so the repo root is one up.
$RepoRoot      = Split-Path -Parent $PSScriptRoot
$Csproj        = Join-Path $RepoRoot 'MarvelHeroes.DpsMeter\MarvelHeroes.DpsMeter.csproj'
$PackageReadme = Join-Path $PSScriptRoot 'PackageReadme.txt'
$PublishRoot   = Join-Path $RepoRoot 'publish'
$StagingDir    = Join-Path $PublishRoot 'MarvelHeroesDpsMeter'
$ZipPath       = Join-Path $PublishRoot ("MarvelHeroesDpsMeter-v{0}.zip" -f $Version)

if (-not (Test-Path $Csproj)) {
    throw ("Could not find {0}. Run this script from inside the repo." -f $Csproj)
}
if (-not (Test-Path $PackageReadme)) {
    throw ("Could not find {0}. Expected next to publish.ps1." -f $PackageReadme)
}

# Heads-up if a previous instance is still running ------------------------
# Release publish targets bin\Release so a running Debug build normally does
# not block the publish, but flag it so the user can close the app if it does.
$running = Get-Process -Name 'MarvelHeroes.DpsMeter' -ErrorAction SilentlyContinue
if ($running) {
    $pidList = ($running | ForEach-Object { $_.Id }) -join ', '
    Write-Warning "MarvelHeroes.DpsMeter.exe is currently running (PID $pidList). The Release publish should still succeed, but close the app if the build fails on a file lock."
}

# Clean previous output ---------------------------------------------------
if (Test-Path $StagingDir) {
    Write-Host ("Cleaning {0} ..." -f $StagingDir)
    Remove-Item -Recurse -Force $StagingDir
}
if (Test-Path $ZipPath) {
    Write-Host ("Removing previous {0} ..." -f $ZipPath)
    Remove-Item -Force $ZipPath
}

# Publish -----------------------------------------------------------------
Write-Host ""
Write-Host ("Publishing self-contained single-file build to {0} ..." -f $StagingDir)
Write-Host ""

$publishArgs = @(
    'publish'
    $Csproj
    '-c', 'Release'
    '-r', 'win-x64'
    '--self-contained', 'true'
    '-p:PublishSingleFile=true'
    '-p:IncludeNativeLibrariesForSelfExtract=true'
    '-p:EnableCompressionInSingleFile=true'
    '-p:DebugType=none'
    '-p:DebugSymbols=false'
    '-p:ContinuousIntegrationBuild=true'
    '-p:DeterministicSourcePaths=true'
    '-p:GenerateDocumentationFile=false'
    '-o', $StagingDir
    '-nologo'
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw ("dotnet publish failed with exit code {0}." -f $LASTEXITCODE)
}

$exe = Join-Path $StagingDir 'MarvelHeroes.DpsMeter.exe'
if (-not (Test-Path $exe)) {
    throw ("Expected EXE not found at {0} after publish." -f $exe)
}

# Stage the readme --------------------------------------------------------
Copy-Item -Force $PackageReadme (Join-Path $StagingDir 'README.txt')

# Sanity check: no PDBs, no loose JSON / XML in the package ---------------
$unexpected = Get-ChildItem $StagingDir -File |
    Where-Object { $_.Extension -in '.pdb', '.xml', '.json' }
if ($unexpected) {
    Write-Warning "Unexpected files in publish output (will still be zipped):"
    $unexpected | ForEach-Object { Write-Warning ("  " + $_.Name) }
}

# Personal-info leak scan -------------------------------------------------
if (-not $SkipLeakScan) {
    Write-Host "Scanning EXE for personal-info patterns ..."
    $bytes = [System.IO.File]::ReadAllBytes($exe)
    $haystacks = @(
        [System.Text.Encoding]::UTF8.GetString($bytes),
        [System.Text.Encoding]::Unicode.GetString($bytes)
    )
    # Pull the current user's name dynamically so the check is portable.
    # NB: the .NET single-file host bootstrap contains a literal string
    # "singlefilehost.pdb" — that's a runtime reference, not a debug symbol
    # we shipped, so we scan for our project's own PDB name instead.
    $userName = $env:USERNAME
    $leakPatterns = @()
    if ($userName) { $leakPatterns += ("\\Users\\{0}\\" -f $userName) }
    $leakPatterns += @(
        'MarvelHeroes\.DpsMeter\.pdb'
        'MarvelHeroesComporator\.NetworkSniffer\.pdb'
        'Gazillion\.pdb'
        '@gmail\.com'
        '@anthropic\.com'
    )

    $leaksFound = $false
    foreach ($pat in $leakPatterns) {
        foreach ($hay in $haystacks) {
            $m = [regex]::Match($hay, $pat, 'IgnoreCase')
            if ($m.Success) {
                $leaksFound = $true
                $start = [Math]::Max(0, $m.Index - 20)
                $len   = [Math]::Min(80, $hay.Length - $start)
                $ctx   = $hay.Substring($start, $len) -replace '[\x00-\x08\x0B\x0C\x0E-\x1F]', '.'
                Write-Warning ("Possible leak - pattern '{0}' matched near: {1}" -f $pat, $ctx)
                break
            }
        }
    }
    if (-not $leaksFound) {
        Write-Host "  [ok] No personal-info patterns found."
    } else {
        Write-Warning ("Review the output above. Run with -SkipLeakScan once you have " +
            "confirmed the matches are benign (e.g. .NET framework strings).")
    }
}

# Zip ---------------------------------------------------------------------
Write-Host ""
Write-Host ("Creating {0} ..." -f $ZipPath)
Compress-Archive -Path (Join-Path $StagingDir '*') -DestinationPath $ZipPath -CompressionLevel Optimal

# Summary -----------------------------------------------------------------
$zipInfo = Get-Item $ZipPath
$exeInfo = Get-Item $exe
$sizeMb  = '{0:N1} MB' -f ($zipInfo.Length / 1MB)
$exeMb   = '{0:N1} MB' -f ($exeInfo.Length / 1MB)

Write-Host ""
Write-Host "-----------------------------------------------------------"
Write-Host ("  Built MarvelHeroesDpsMeter v{0}" -f $Version)
Write-Host "-----------------------------------------------------------"
Write-Host ("  EXE : {0}" -f $exe)
Write-Host ("        {0} (self-contained, single file, no .NET install needed)" -f $exeMb)
Write-Host ("  Zip : {0}" -f $ZipPath)
Write-Host ("        {0}" -f $sizeMb)
Write-Host ""
Write-Host "  Send the zip to your friend. Tell them to install Npcap"
Write-Host "  (https://npcap.com/#download), unzip, and double-click the EXE."
Write-Host "-----------------------------------------------------------"
