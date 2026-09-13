# SPDX-License-Identifier: LGPL-3.0-or-later
#
# Builds the current checkout and deploys it to the theater (D-037): client bundle,
# published server, restart, verification. The same sequence that was run by hand for
# every deploy through 2026-08-30, now in one place.
#
# The published copy lives outside the repository on purpose: a Barkeep running from
# the build tree holds file locks that make later builds fail silently, and the
# operator's firewall allow rule is scoped to the published path.

[CmdletBinding()]
param(
    [string]$AppDirectory = (Join-Path $env:ProgramData 'Cantina\app'),
    [string]$SettingsPath = (Join-Path $env:ProgramData 'Cantina\theater.json'),
    [string]$VerifyUrl = 'https://cantina.aero4ge.com/'
)

$ErrorActionPreference = 'Stop'
$repository = Resolve-Path (Join-Path $PSScriptRoot '..\..')

Push-Location (Join-Path $repository 'src\cantina-client')
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'The client build failed; nothing was deployed.' }
}
finally {
    Pop-Location
}

# Publish before stopping, so a failed build never leaves the theater down.
$staging = Join-Path ([IO.Path]::GetTempPath()) "cantina-publish-$([Guid]::NewGuid().ToString('N'))"
dotnet publish (Join-Path $repository 'src\Cantina.Barkeep\Cantina.Barkeep.csproj') `
    --configuration Release --output $staging
if ($LASTEXITCODE -ne 0) { throw 'The server publish failed; the running theater was not touched.' }

$executable = Join-Path $AppDirectory 'Cantina.Barkeep.exe'
Get-Process -Name 'Cantina.Barkeep' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and ([IO.Path]::GetFullPath($_.Path) -ieq [IO.Path]::GetFullPath($executable)) } |
    ForEach-Object { Stop-Process -Id $_.Id -Force; $_.WaitForExit(10000) | Out-Null }

New-Item -ItemType Directory -Force $AppDirectory | Out-Null
Copy-Item (Join-Path $staging '*') $AppDirectory -Recurse -Force
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue

& (Join-Path $PSScriptRoot 'Start-Barkeep.ps1') -AppDirectory $AppDirectory -SettingsPath $SettingsPath

# Verify by outcome, not by having launched a process.
$deadline = (Get-Date).AddSeconds(30)
do {
    Start-Sleep -Seconds 2
    $code = & "$env:SystemRoot\System32\curl.exe" -s -o NUL -m 5 -w '%{http_code}' $VerifyUrl
} while ($code -ne '200' -and (Get-Date) -lt $deadline)

if ($code -ne '200') {
    throw "Deployed, but $VerifyUrl answered '$code'. Check the newest log in $env:ProgramData\Cantina\logs."
}

"Deployed and answering: $VerifyUrl"
