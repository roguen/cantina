# SPDX-License-Identifier: LGPL-3.0-or-later
#
# Issue #4: is there a metadata key that yields an unambiguous song query?
#
# Reads song.ini files directly. Does not read YARG's cache, modify anything, or emit song
# content. Output is counts plus the field values needed to explain a collision.

param(
    [Parameter(Mandatory = $true)]
    [string] $Root
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Root)) {
    Write-Error "song root not found: $Root"
    exit 2
}

$inis = Get-ChildItem -LiteralPath $Root -Recurse -Filter 'song.ini' -File -ErrorAction SilentlyContinue
$songs = New-Object System.Collections.Generic.List[object]

foreach ($file in $inis) {
    $fields = @{}
    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        if ($line -match '^\s*([A-Za-z_]+)\s*=\s*(.*)$') {
            $key = $Matches[1].ToLowerInvariant()
            if (-not $fields.ContainsKey($key)) { $fields[$key] = $Matches[2].Trim() }
        }
    }

    if ($fields.ContainsKey('name')) {
        $songs.Add([PSCustomObject]@{
                Title   = $fields['name']
                Artist  = $fields['artist']
                Charter = $fields['charter']
                Source  = $fields['source']
            })
    }
}

"song.ini files : $($inis.Count)"
"parsed         : $($songs.Count)"
''

$pairKey = { '{0}|{1}' -f $_.Title.ToLowerInvariant(), ('' + $_.Artist).ToLowerInvariant() }
$ambiguous = $songs | Group-Object $pairKey | Where-Object { $_.Count -gt 1 }
$involved = ($ambiguous | Measure-Object -Property Count -Sum).Sum

"ambiguous by title+artist : $($ambiguous.Count) groups, $involved songs"

$stillAfterCharter = 0
$stillAfterBoth = 0

foreach ($group in $ambiguous) {
    $byCharter = $group.Group | Group-Object { ('' + $_.Charter).ToLowerInvariant() }
    if (($byCharter | Where-Object { $_.Count -gt 1 }).Count -gt 0) { $stillAfterCharter++ }

    $byBoth = $group.Group | Group-Object {
        '{0}|{1}' -f ('' + $_.Charter).ToLowerInvariant(), ('' + $_.Source).ToLowerInvariant()
    }
    if (($byBoth | Where-Object { $_.Count -gt 1 }).Count -gt 0) { $stillAfterBoth++ }
}

"still ambiguous + charter        : $stillAfterCharter of $($ambiguous.Count)"
"still ambiguous + charter+source : $stillAfterBoth of $($ambiguous.Count)"
''
"empty source fields : $(($songs | Where-Object { -not $_.Source }).Count) of $($songs.Count)"

$markup = $songs | Where-Object { $_.Charter -match '<[^>]+>' -or $_.Title -match '<[^>]+>' }
"values containing rich-text markup : $($markup.Count)"

if ($stillAfterBoth -gt 0) {
    ''
    "RESULT: no available metadata combination is a unique key."
    "$stillAfterBoth group(s) cannot be separated by title, artist, charter, or source."
    exit 1
}

''
"RESULT: title+artist+charter+source separates every group in this library."
exit 0
