# SPDX-License-Identifier: LGPL-3.0-or-later
#
# Starts the theater's Barkeep if it is not already running (D-037).
#
# Idempotent by design: the startup task runs this at sign-in AND every few minutes, so
# the same script is both the boot path and the crash watchdog. When Barkeep is already
# up it exits without touching anything.
#
# It must run in the operator's interactive session, never as a service: Barkeep sends
# keystrokes to YARG, and input injected from session 0 reaches no desktop (D-024).

[CmdletBinding()]
param(
    [string]$AppDirectory = (Join-Path $env:ProgramData 'Cantina\app'),
    [string]$SettingsPath = (Join-Path $env:ProgramData 'Cantina\theater.json'),
    [string]$LogDirectory = (Join-Path $env:ProgramData 'Cantina\logs'),
    [int]$KeepLogs = 10
)

$ErrorActionPreference = 'Stop'
$executable = Join-Path $AppDirectory 'Cantina.Barkeep.exe'

if (-not (Test-Path $executable)) {
    throw "No published Barkeep at $executable. Run Deploy-Theater.ps1 first."
}

if (-not (Test-Path $SettingsPath)) {
    throw "No theater settings at $SettingsPath. Copy theater.example.json there and fill it in."
}

# Already running from this exact path: nothing to do. Matching the path, not just the
# name, keeps a development copy started from the repository from masking a dead theater.
$running = Get-Process -Name 'Cantina.Barkeep' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and ([IO.Path]::GetFullPath($_.Path) -ieq [IO.Path]::GetFullPath($executable)) }

if ($running) {
    Write-Verbose "Barkeep is already running (pid $($running[0].Id))."
    return
}

New-Item -ItemType Directory -Force $LogDirectory | Out-Null

# One log per start, newest kept. The console is where Barkeep prints pairing codes and
# certificate warnings, and a hidden process has no console to read them from.
Get-ChildItem $LogDirectory -Filter 'barkeep-*.log' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip ($KeepLogs - 1) |
    Remove-Item -Force -ErrorAction SilentlyContinue

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$log = Join-Path $LogDirectory "barkeep-$stamp.log"
$errors = Join-Path $LogDirectory "barkeep-$stamp.err.log"

Start-Process -FilePath $executable `
    -ArgumentList "--TheaterConfig=`"$SettingsPath`"" `
    -WorkingDirectory $AppDirectory `
    -WindowStyle Hidden `
    -RedirectStandardOutput $log `
    -RedirectStandardError $errors | Out-Null

Write-Verbose "Barkeep started; logging to $log."
