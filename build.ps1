param(
    [switch]$TestOnly
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src'
$bin = Join-Path $root 'bin'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (!(Test-Path $csc)) {
    throw "C# compiler not found at $csc"
}

New-Item -ItemType Directory -Force -Path $bin | Out-Null
if (Test-Path (Join-Path $root 'tests')) {
    Copy-Item -Path (Join-Path $root 'tests') -Destination $bin -Recurse -Force
}

function Invoke-Csc {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & $csc @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "csc failed with exit code $LASTEXITCODE"
    }
}

$core = Join-Path $bin 'AmfQuickLook.Core.dll'
$app = Join-Path $bin 'AmfQuickLook.exe'
$shell = Join-Path $bin 'AmfQuickLook.Shell.dll'
$tests = Join-Path $bin 'AmfQuickLook.Tests.exe'

Invoke-Csc /nologo /target:library /optimize+ /platform:x64 `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /r:System.Xml.Linq.dll `
    /out:$core (Join-Path $src 'AmfCore.cs')

Invoke-Csc /nologo /target:exe /optimize+ /platform:x64 `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Xml.dll /r:$core `
    /out:$tests (Join-Path $src 'AmfQuickLook.Tests.cs')

& $tests

if (!$TestOnly) {
    Invoke-Csc /nologo /target:winexe /optimize+ /platform:x64 `
        /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /r:$core `
        /out:$app (Join-Path $src 'AmfQuickLookApp.cs')

    Invoke-Csc /nologo /target:library /optimize+ /platform:x64 `
        /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /r:System.Xml.Linq.dll `
        /out:$shell (Join-Path $src 'AmfShell.cs') (Join-Path $src 'AmfCore.cs')

    $shellRefs = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($shell).GetReferencedAssemblies() | ForEach-Object { $_.Name }
    if ($shellRefs -contains 'AmfQuickLook.Core') {
        throw 'Shell preview DLL must be self-contained and must not reference AmfQuickLook.Core.dll.'
    }

    Write-Host "Built:"
    Write-Host "  $app"
    Write-Host "  $shell"
}
