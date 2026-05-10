#requires -Version 5.1

<#
.SYNOPSIS
    Generate Services/BossNames.cs from the comments in Services/BossPrototypes.cs.

.DESCRIPTION
    BossPrototypes.cs stores every Boss / GroupBoss / MiniBoss prototype enum index in a
    HashSet, with the full prototype path included as a trailing comment, e.g.:

        267u,  // [GroupBoss] Entity/Characters/Bosses/PVEWaveBattle/EGWB01Juggernaut.prototype

    This script lifts those paths, derives a human-readable display name for each one, and
    writes them to BossNames.cs as a Dictionary<uint, string> so the DPS meter can surface
    "Juggernaut" in the fight title instead of just "Boss Fight".

    Cleanup heuristics (applied in order):
      - Strip TRNamed / TRName / Named / TR prefixes (trash-tier mob tags)
      - Strip event-tag prefixes:  EGD\d+[A-Z]* / EGWB\d+ / EGW\d+[A-Z]* / EGB\d+ / EG\d+[A-Z]*
      - Strip trailing chapter / phase / encounter codes:
          (Ch|CH|Chapter)\d+[A-Z]?  /  Enc\d+[A-Z]?  /  Phase\d+  /  D\d+  /  Round\d+  /
          Affix\d+  /  SpawnIn\w*
      - Split CamelCase to "Camel Case"
      - Collapse repeated whitespace; trim

    The generator runs only when invoked; it does not run as part of the build.  Re-run after
    regenerating BossPrototypes.cs from a fresh dump.

.PARAMETER InputFile
    Path to BossPrototypes.cs.  Defaults to MarvelHeroes.DpsMeter/Services/BossPrototypes.cs
    relative to the repo root.

.PARAMETER OutputFile
    Path to write BossNames.cs.  Defaults to MarvelHeroes.DpsMeter/Services/BossNames.cs.

.PARAMETER DryRun
    Don't write the output; print stats and a sample of cleaned names to the console.
#>

[CmdletBinding()]
param(
    [string] $InputFile,
    [string] $OutputFile,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not $InputFile)  { $InputFile  = Join-Path $RepoRoot 'MarvelHeroes.DpsMeter\Services\BossPrototypes.cs' }
if (-not $OutputFile) { $OutputFile = Join-Path $RepoRoot 'MarvelHeroes.DpsMeter\Services\BossNames.cs' }

if (-not (Test-Path $InputFile)) {
    throw "Input file not found: $InputFile"
}

# Match a single dumper line.  Captures:
#   1 = proto index
#   2 = tag (Boss / GroupBoss / MiniBoss)
#   3 = full prototype path (without the trailing ".prototype")
$lineRegex = '^\s+(\d+)u,\s*//\s*\[(Boss|GroupBoss|MiniBoss)\]\s+(\S+)\.prototype\s*$'

function Clean-Name([string] $rawBasename) {
    $n = $rawBasename

    # ── Important: -creplace (not -replace) for every step that uses [A-Z] or [a-z].
    # PowerShell's -replace is case-INsensitive by default, so [A-Z] matches lowercase too
    # and a greedy [A-Z]* eats the entire rest of the string.  -creplace forces case
    # sensitivity so the character classes mean what they say.

    # Leading prefixes (mob/event tags).  The negative lookahead on the trailing [A-Z]* is
    # critical: without it, "EGD11GLokiPhase1" matches "EGD11GL" (greedy uppercase consumes
    # the L of Loki) and the regex eats the start of the actual name.  (?![a-z]) forces the
    # tag-letter run to STOP one character before a CamelCase word starts.
    $n = $n -creplace '^(TRNamedElite|TRNamed|TRName|TR)', ''
    $n = $n -creplace '^(EGWB|EGB|EGW|EGD|EG)\d+[A-Z]*(?![a-z])', ''
    $n = $n -creplace '^Named', ''

    # Trailing suffixes (chapter / phase / encounter / variant codes).  Order matters -- strip
    # the most specific tokens first so the leftover bare-number cases don't take precedence.
    $n = $n -creplace '(Chapter|Ch|CH)\d+[A-Z]?$', ''
    $n = $n -creplace 'Enc\d+[A-Z]?$', ''
    $n = $n -creplace 'Phase\d+$', ''
    $n = $n -creplace 'Round\d+$', ''
    $n = $n -creplace 'Affix\d+$', ''
    $n = $n -creplace 'SpawnIn[A-Za-z0-9]*$', ''
    $n = $n -creplace '(Easy|Hard|Heroic|Cosmic|Green|Red|Blue)?D\d+[A-Z]?$', ''

    # If we stripped everything, fall back to the original basename (better than '').
    if ($n -eq '') { $n = $rawBasename }

    # CamelCase -> "Camel Case".  Insert a space before each capital that follows a lowercase
    # letter or a digit; also between consecutive caps when the next char is lowercase (so
    # "DangerRoomDocOctopus" -> "Danger Room Doc Octopus" not "Dr Octopus" mangled).
    $n = [Regex]::Replace($n, '([a-z\d])([A-Z])', '$1 $2')
    $n = [Regex]::Replace($n, '([A-Z]+)([A-Z][a-z])', '$1 $2')

    # Collapse repeated whitespace, trim.  Whitespace doesn't care about case.
    $n = ($n -replace '\s+', ' ').Trim()
    return $n
}

# --- Parse the input file ------------------------------------------------
$entries = New-Object System.Collections.Generic.List[psobject]
$lineNo = 0
foreach ($line in [System.IO.File]::ReadAllLines($InputFile)) {
    $lineNo++
    $m = [Regex]::Match($line, $lineRegex)
    if (-not $m.Success) { continue }

    $idx  = [uint32] $m.Groups[1].Value
    $tag  = $m.Groups[2].Value
    $path = $m.Groups[3].Value
    $base = $path.Split('/')[-1]
    $name = Clean-Name $base

    $entries.Add([pscustomobject]@{
        Index    = $idx
        Tag      = $tag
        Path     = $path
        Basename = $base
        Name     = $name
        LineNo   = $lineNo
    }) | Out-Null
}

if ($entries.Count -eq 0) {
    throw "No prototype entries matched in $InputFile.  Check that the file still uses the '[Tag] Entity/...prototype' comment format."
}

# Boss + GroupBoss + MiniBoss can all show up as fight targets, so we keep them all.  De-dup
# by index just in case the dumper emitted the same record twice; first occurrence wins.
$byIndex = @{}
foreach ($e in $entries) {
    if (-not $byIndex.ContainsKey($e.Index)) {
        $byIndex[$e.Index] = $e
    }
}

# --- Stats / sample -----------------------------------------------------
Write-Host ("Parsed {0} entries from {1} ({2} unique indices)" -f $entries.Count, (Split-Path -Leaf $InputFile), $byIndex.Count)
$tagCounts = $entries | Group-Object Tag | Sort-Object Name | ForEach-Object { "{0}: {1}" -f $_.Name, $_.Count }
Write-Host ("Tags: {0}" -f ($tagCounts -join '  |  '))

Write-Host ""
Write-Host "Sample (first 15 cleaned names):"
$entries | Select-Object -First 15 | ForEach-Object {
    "  {0,5}  [{1,-9}]  {2,-50}  =>  {3}" -f $_.Index, $_.Tag, $_.Basename, $_.Name
} | Write-Host

if ($DryRun) {
    Write-Host ""
    Write-Host "Dry run -- output file NOT written."
    return
}

# --- Emit BossNames.cs --------------------------------------------------
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('using System.Collections.Generic;')
[void]$sb.AppendLine()
[void]$sb.AppendLine('namespace MarvelHeroes.DpsMeter.Services;')
[void]$sb.AppendLine()
[void]$sb.AppendLine('/// <summary>')
[void]$sb.AppendLine('/// Maps a boss <c>prototypeEnumIndex</c> (the same key used in <see cref="BossPrototypes.Indices"/>')
[void]$sb.AppendLine('/// and <see cref="BossPrototypes.MiniBossIndices"/>) to a human-readable display name --')
[void]$sb.AppendLine('/// e.g. <c>267u -> "Juggernaut"</c>.  Surfaced in the live overlay title during an active')
[void]$sb.AppendLine('/// boss fight, and baked into <c>DpsSnapshot.BossName</c> on auto-save so the fight history')
[void]$sb.AppendLine('/// shows which boss the run was against rather than just "Boss Fight".')
[void]$sb.AppendLine('///')
[void]$sb.AppendLine('/// <para>Generated by <c>scripts/GenerateBossNames.ps1</c> from the prototype paths in')
[void]$sb.AppendLine('/// <see cref="BossPrototypes"/>.  Re-run after regenerating BossPrototypes from a fresh')
[void]$sb.AppendLine('/// <c>OpenCalligraphy.AvatarEnumDumper</c> output.  Names are auto-derived from the path')
[void]$sb.AppendLine('/// basename with content-tag prefixes / chapter suffixes stripped -- for tricky cases edit')
[void]$sb.AppendLine('/// either this file directly or improve the cleanup heuristics in the generator.</para>')
[void]$sb.AppendLine('///')
[void]$sb.AppendLine('/// <para>Same dumper off-by-one caveat as <see cref="BossPrototypes.IsBoss"/>: indices &gt;=')
[void]$sb.AppendLine('/// 10000 may be one less on disk than what the live network sends, so callers should')
[void]$sb.AppendLine('/// probe both <c>idx</c> and <c>idx - 1</c> when resolving.  <see cref="Get"/> does this for you.</para>')
[void]$sb.AppendLine('/// </summary>')
[void]$sb.AppendLine('internal static class BossNames')
[void]$sb.AppendLine('{')
[void]$sb.AppendLine('    private const uint DumperOffByOneThreshold = 10000u;')
[void]$sb.AppendLine()
[void]$sb.AppendLine('    /// <summary>Returns the display name for <paramref name="prototypeEnumIndex"/>, or')
[void]$sb.AppendLine('    /// <c>null</c> when the index is unmapped.  Applies the same off-by-one workaround')
[void]$sb.AppendLine('    /// for indices &gt;= <see cref="DumperOffByOneThreshold"/> that <see cref="BossPrototypes.IsBoss"/>')
[void]$sb.AppendLine('    /// uses, so callers don''t need to special-case the high-index range.</summary>')
[void]$sb.AppendLine('    public static string? Get(uint prototypeEnumIndex)')
[void]$sb.AppendLine('    {')
[void]$sb.AppendLine('        if (s_names.TryGetValue(prototypeEnumIndex, out var n)) return n;')
[void]$sb.AppendLine('        if (prototypeEnumIndex >= DumperOffByOneThreshold')
[void]$sb.AppendLine('            && s_names.TryGetValue(prototypeEnumIndex - 1u, out var n2)) return n2;')
[void]$sb.AppendLine('        return null;')
[void]$sb.AppendLine('    }')
[void]$sb.AppendLine()
[void]$sb.AppendLine('    private static readonly Dictionary<uint, string> s_names = new()')
[void]$sb.AppendLine('    {')

$sorted = $byIndex.Values | Sort-Object Index
foreach ($e in $sorted) {
    $escaped = $e.Name -replace '\\', '\\\\' -replace '"', '\"'
    $line = "        {{ {0,6}u, ""{1}"" }},  // [{2}] {3}.prototype" -f $e.Index, $escaped, $e.Tag, $e.Path
    [void]$sb.AppendLine($line)
}

[void]$sb.AppendLine('    };')
[void]$sb.AppendLine('}')

# Ensure parent dir exists, write with UTF-8 BOM so MSBuild + Roslyn read it predictably.
$dir = Split-Path -Parent $OutputFile
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$utf8Bom = New-Object System.Text.UTF8Encoding($true)
[System.IO.File]::WriteAllText($OutputFile, $sb.ToString(), $utf8Bom)

Write-Host ""
Write-Host ("Wrote {0} mappings to {1}" -f $byIndex.Count, $OutputFile)
