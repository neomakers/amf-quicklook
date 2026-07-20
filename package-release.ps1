param(
    [string]$Version = '0.1.3'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root 'dist'
$packageName = "AMFQuickLook-v$Version-win-x64"
$staging = Join-Path $dist $packageName
$zipPath = Join-Path $dist "$packageName.zip"
$setupPath = Join-Path $dist "AMFQuickLookSetup-v$Version-win-x64.exe"
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'build.ps1')
if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

if (Test-Path $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}
if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path $setupPath) {
    Remove-Item -LiteralPath $setupPath -Force
}

New-Item -ItemType Directory -Force -Path $staging | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $staging 'bin') | Out-Null

Copy-Item -LiteralPath (Join-Path $root 'bin\AmfQuickLook.Core.dll') -Destination (Join-Path $staging 'bin') -Force
Copy-Item -LiteralPath (Join-Path $root 'bin\AmfQuickLook.exe') -Destination (Join-Path $staging 'bin') -Force
Copy-Item -LiteralPath (Join-Path $root 'bin\AmfQuickLook.Shell.dll') -Destination (Join-Path $staging 'bin') -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $staging -Force
Copy-Item -LiteralPath (Join-Path $root 'install.ps1') -Destination $staging -Force
Copy-Item -LiteralPath (Join-Path $root 'uninstall.ps1') -Destination $staging -Force
Copy-Item -LiteralPath (Join-Path $root 'Install-AMFQuickLook.cmd') -Destination $staging -Force
Copy-Item -LiteralPath (Join-Path $root 'Uninstall-AMFQuickLook.cmd') -Destination $staging -Force

$releaseReadme = @"
AMF QuickLook v$Version for Windows x64

Quick install:
1. Extract this zip to a normal folder.
2. Double-click Install-AMFQuickLook.cmd.
3. Reopen Explorer and press Alt+P for Preview Pane.

Manual install:
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1

Uninstall:
Double-click Uninstall-AMFQuickLook.cmd
"@

Set-Content -LiteralPath (Join-Path $staging 'INSTALL.txt') -Value $releaseReadme -Encoding ASCII

Compress-Archive -LiteralPath $staging -DestinationPath $zipPath -CompressionLevel Optimal

$setupArgs = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/platform:x64',
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Windows.Forms.dll',
    '/r:Microsoft.CSharp.dll',
    "/resource:$((Join-Path $root 'bin\AmfQuickLook.Core.dll')),payload.AmfQuickLook.Core.dll",
    "/resource:$((Join-Path $root 'bin\AmfQuickLook.exe')),payload.AmfQuickLook.exe",
    "/resource:$((Join-Path $root 'bin\AmfQuickLook.Shell.dll')),payload.AmfQuickLook.Shell.dll",
    "/resource:$((Join-Path $root 'install.ps1')),payload.install.ps1",
    "/resource:$((Join-Path $root 'uninstall.ps1')),payload.uninstall.ps1",
    "/resource:$((Join-Path $root 'README.md')),payload.README.md",
    "/out:$setupPath",
    (Join-Path $root 'src\AmfQuickLookSetup.cs')
)
& $csc @setupArgs
if ($LASTEXITCODE -ne 0) {
    throw "Setup compiler failed."
}

Write-Host $zipPath
Write-Host $setupPath
