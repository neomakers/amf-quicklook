# AMF QuickLook

AMF QuickLook is a lightweight Windows previewer for `.amf` / `.AMF` additive-manufacturing model files.

It provides:

- double-click viewing through a small WinForms 3D viewer;
- mouse rotate, mouse wheel zoom, and right-drag pan;
- PNG export and command-line thumbnail generation;
- per-user Explorer Preview Pane and thumbnail registration;
- plain XML AMF and ZIP-compressed AMF support, including FreeCAD exports;
- XML/text Preview Pane fallback when Windows refuses to load custom shell handlers.

## Requirements

- Windows
- .NET Framework 4.x runtime/compiler, normally already present at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`
- PowerShell

No Python, Node.js, or NuGet packages are required.

## Build

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The build writes binaries to `bin\` and runs the included tests.

## Install For Current User

For normal use, download the latest `AMFQuickLookSetup-*-win-x64.exe` from GitHub Releases and double-click it.

The installer copies AMF QuickLook to `%LocalAppData%\Programs\AMF QuickLook`, creates Start Menu and desktop shortcuts, registers `.amf`, and adds a Windows uninstall entry.

The release also includes a zip package for portable/manual installation.

For source checkouts, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

After install:

- double-click an `.amf` file to open AMF QuickLook;
- press `Alt+P` in Explorer to show the Preview Pane;
- reopen Explorer if thumbnails or preview handlers do not appear immediately.

If Windows already has a protected `UserChoice` file association for `.amf`, right-click an AMF file, choose **Open with**, then choose AMF QuickLook once.

## Uninstall

From the release zip, double-click `Uninstall-AMFQuickLook.cmd`.

From a source checkout, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\uninstall.ps1
```

The uninstall script removes only registry keys owned by AMF QuickLook.

## Command-Line Thumbnail

```powershell
.\bin\AmfQuickLook.exe --thumbnail .\tests\fixtures\cube_multi.AMF .\preview.png 512
```

## Build A Release Zip

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\package-release.ps1 -Version 0.1.3
```

The package script writes both:

- `dist\AMFQuickLookSetup-v0.1.3-win-x64.exe`
- `dist\AMFQuickLook-v0.1.3-win-x64.zip`

## Notes

Windows Shell extensions are subject to Explorer caching and local policy. The installer registers a custom preview handler and thumbnail provider, and also sets `.amf` as XML/text so Preview Pane remains useful even if Windows blocks custom handler activation.
