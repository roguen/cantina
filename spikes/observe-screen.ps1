# SPDX-License-Identifier: LGPL-3.0-or-later
#
# Captures the screen so a spike result that is only visible on the projector can be read.
#
# This exists because YARG's menu state is invisible on the wire. The datagram reports
# CurrentScene = Menu for the start menu, the song list, settings, and instrument setup
# alike (D-015), and whether typed text reached a search field is not in the datagram at
# all. Anything that cannot be observed is not evidence, so the screen is the oracle of
# last resort.
#
# It only ever reads. Nothing here sends input or changes YARG.
#
# Captures may show local song libraries and window contents, so they are written under
# spikes/captures/, which is gitignored for exactly that reason.
#
#   ./observe-screen.ps1 -Label before-typing
#   ./observe-screen.ps1 -Label after-typing -Region 0,0,1920,200

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Label,
    [string]$OutputDirectory,
    [int[]]$Region
)

Add-Type -AssemblyName System.Windows.Forms, System.Drawing

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path (Split-Path $PSCommandPath) 'captures'
}

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
}

$screen = [System.Windows.Forms.SystemInformation]::VirtualScreen

if ($Region -and $Region.Count -eq 4) {
    $x, $y, $width, $height = $Region
} else {
    $x = $screen.Left; $y = $screen.Top
    $width = $screen.Width; $height = $screen.Height
}

$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

try {
    $graphics.CopyFromScreen($x, $y, 0, 0, $bitmap.Size)
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $path = Join-Path $OutputDirectory "screen-$stamp-$Label.png"
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}

# Report which window was actually in front. A capture of the wrong window is the most
# likely way this tool lies, and it is silent unless stated.
$signature = @'
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out uint pid);
'@
$win32 = Add-Type -MemberDefinition $signature -Name ScreenObserve -Namespace Cantina -PassThru
$processId = 0
$null = $win32::GetWindowThreadProcessId($win32::GetForegroundWindow(), [ref]$processId)
$foreground = (Get-Process -Id $processId -ErrorAction SilentlyContinue).ProcessName

[PSCustomObject]@{
    Path       = $path
    Region     = "${width}x${height} at ${x},${y}"
    Foreground = "$foreground (pid $processId)"
}
