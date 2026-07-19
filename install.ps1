$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$build = Join-Path $root 'build.ps1'
$bin = Join-Path $root 'bin'
$exe = Join-Path $bin 'AmfQuickLook.exe'
$shellDll = Join-Path $bin 'AmfQuickLook.Shell.dll'
$classes = 'HKCU:\Software\Classes'

$progId = 'AmfQuickLook.File'
$thumbClsid = '{84F9DD9B-6C88-45D0-86E4-2D3D9447113D}'
$previewClsid = '{FD3E123C-D4F0-4BC7-97D4-142C4FB4F035}'
$thumbnailHandlerIid = '{e357fccd-a995-4576-b01f-234630154e96}'
$previewHandlerIid = '{8895b1c6-b41f-4c1c-a562-0d564250836f}'
$txtPreviewClsid = '{1531d583-8375-4d3f-b5fb-d23bbd169f22}'

if (!(Test-Path $exe) -or !(Test-Path $shellDll)) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $build
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
}

function New-Key($path) {
    if (!(Test-Path $path)) {
        New-Item -Path $path -Force | Out-Null
    }
}

function Convert-RegPath($path) {
    if ($path.StartsWith('HKCU:\')) {
        return 'HKCU\' + $path.Substring(6)
    }
    if ($path.StartsWith('HKLM:\')) {
        return 'HKLM\' + $path.Substring(6)
    }
    return $path
}

function Invoke-Reg {
    param([string[]]$RegArgs)
    & reg.exe @RegArgs | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "reg.exe failed: $($RegArgs -join ' ')"
    }
}

function Set-Default($path, $value) {
    New-Key $path
    Set-Item -Path $path -Value $value
}

function Set-String($path, $name, $value) {
    New-Key $path
    Invoke-Reg -RegArgs @('add', (Convert-RegPath $path), '/v', $name, '/t', 'REG_SZ', '/d', $value, '/f')
}

function Set-EmptyString($path, $name) {
    New-Key $path
    New-ItemProperty -Path $path -Name $name -Value '' -PropertyType String -Force | Out-Null
}

function Register-DotNetCom($clsid, $name, $className, $dllPath) {
    $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($dllPath).FullName
    $codeBase = $dllPath
    $base = Join-Path $classes "CLSID\$clsid"
    $inproc = Join-Path $base 'InprocServer32'
    Set-Default $base $name
    Set-Default $inproc 'mscoree.dll'
    Set-String $inproc 'ThreadingModel' 'Both'
    Set-String $inproc 'Class' $className
    Set-String $inproc 'Assembly' $assemblyName
    Set-String $inproc 'RuntimeVersion' 'v4.0.30319'
    Set-String $inproc 'CodeBase' $codeBase
}

$extension = Join-Path $classes '.amf'
Set-Default $extension $progId
Set-String $extension 'Content Type' 'application/xml'
Set-String $extension 'PerceivedType' 'text'
Set-EmptyString (Join-Path $extension 'OpenWithProgids') $progId
Set-Default (Join-Path $extension "shellex\$previewHandlerIid") $previewClsid
Set-Default (Join-Path $extension "shellex\$thumbnailHandlerIid") $thumbClsid

$fileType = Join-Path $classes $progId
Set-Default $fileType 'AMF 3D Model'
Set-Default (Join-Path $fileType 'DefaultIcon') "`"$exe`",0"
Set-Default (Join-Path $fileType 'shell\open\command') "`"$exe`" `"%1`""
Set-Default (Join-Path $fileType "shellex\$previewHandlerIid") $previewClsid
Set-Default (Join-Path $fileType "shellex\$thumbnailHandlerIid") $thumbClsid

Register-DotNetCom $thumbClsid 'AMF QuickLook Thumbnail Provider' 'AmfQuickLook.Shell.ThumbnailProvider' $shellDll
Register-DotNetCom $previewClsid 'AMF QuickLook Preview Handler' 'AmfQuickLook.Shell.PreviewHandler' $shellDll

Set-String 'HKCU:\Software\Microsoft\Windows\CurrentVersion\PreviewHandlers' $previewClsid 'AMF QuickLook Preview Handler'
Set-String 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved' $thumbClsid 'AMF QuickLook Thumbnail Provider'
Set-String 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved' $previewClsid 'AMF QuickLook Preview Handler'

# Fallback: if Windows refuses to load the custom preview handler, this keeps AMF readable in Preview Pane as XML/text.
Set-Default (Join-Path $extension "shellex\FallbackTextPreviewHandler") $txtPreviewClsid

Write-Host "AMF QuickLook installed for this Windows user."
Write-Host "Default opener: $exe"
Write-Host "If Explorer was already open, close and reopen that folder. Thumbnail cache may take a moment to refresh."
Write-Host "If Windows has a UserChoice association for .amf, use Open with > Choose another app > AMF QuickLook once."
