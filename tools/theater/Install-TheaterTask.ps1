# SPDX-License-Identifier: LGPL-3.0-or-later
#
# Registers (or removes) the scheduled task that keeps the theater's Barkeep running
# (D-037). Runs as the signed-in operator, needs no elevation, and changes nothing else
# on the machine.
#
#   .\Install-TheaterTask.ps1             register or update the task
#   .\Install-TheaterTask.ps1 -Remove     unregister it
#
# The task has two triggers: at the operator's sign-in (the boot path) and every five
# minutes after that (the watchdog). Both run Start-Barkeep.ps1, which does nothing when
# Barkeep is already up.
#
# Deliberately NOT a Windows service and NOT "run whether the user is logged on or not":
# both put Barkeep in a session where its keystrokes reach no desktop (D-024). The
# consequence is recorded in docs/theater-deployment.md: after a reboot, the theater
# comes up when the operator's account signs in.

[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$TaskName = 'Cantina Barkeep',
    [int]$WatchdogMinutes = 5,
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

if ($Remove) {
    if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
        if ($PSCmdlet.ShouldProcess($TaskName, 'Unregister scheduled task')) {
            Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
            "Removed the '$TaskName' task. A running Barkeep is left running."
        }
    }
    else {
        "No '$TaskName' task is registered."
    }
    return
}

$launcher = Join-Path $PSScriptRoot 'Start-Barkeep.ps1'
$installed = Join-Path $env:ProgramData 'Cantina\tools\Start-Barkeep.ps1'

# The task runs a copy outside the repository, so moving, cleaning, or switching
# branches in the working tree can never break the theater's boot path.
if ($PSCmdlet.ShouldProcess($installed, 'Install launcher copy')) {
    New-Item -ItemType Directory -Force (Split-Path $installed) | Out-Null
    Copy-Item $launcher $installed -Force
}

$user = [Security.Principal.WindowsIdentity]::GetCurrent().Name

$action = New-ScheduledTaskAction `
    -Execute 'powershell.exe' `
    -Argument "-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$installed`""

$atSignIn = New-ScheduledTaskTrigger -AtLogOn -User $user
$watchdog = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) `
    -RepetitionInterval (New-TimeSpan -Minutes $WatchdogMinutes)

$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited

# The launcher exits within seconds, so the defaults that bite long-running tasks (the
# 72-hour execution limit, stop-on-battery) are overridden anyway: a watchdog that
# Windows silently retires is worse than none.
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 2)

if ($PSCmdlet.ShouldProcess($TaskName, 'Register scheduled task')) {
    Register-ScheduledTask -TaskName $TaskName `
        -Action $action `
        -Trigger @($atSignIn, $watchdog) `
        -Principal $principal `
        -Settings $settings `
        -Description 'Keeps the Cantina theater server (Barkeep) running. See docs/theater-deployment.md.' `
        -Force | Out-Null

    "Registered '$TaskName': at sign-in of $user, and every $WatchdogMinutes minutes."
}
