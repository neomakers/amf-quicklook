$ErrorActionPreference = 'Stop'

$classes = 'HKCU:\Software\Classes'
$progId = 'AmfQuickLook.File'
$thumbClsid = '{84F9DD9B-6C88-45D0-86E4-2D3D9447113D}'
$previewClsid = '{FD3E123C-D4F0-4BC7-97D4-142C4FB4F035}'

function Remove-Key($path) {
    if (Test-Path $path) {
        Remove-Item -Path $path -Recurse -Force
    }
}

function Remove-Value($path, $name) {
    if (Test-Path $path) {
        Remove-ItemProperty -Path $path -Name $name -ErrorAction SilentlyContinue
    }
}

Remove-Key (Join-Path $classes '.amf')
Remove-Key (Join-Path $classes $progId)
Remove-Key (Join-Path $classes "CLSID\$thumbClsid")
Remove-Key (Join-Path $classes "CLSID\$previewClsid")
Remove-Value 'HKCU:\Software\Microsoft\Windows\CurrentVersion\PreviewHandlers' $previewClsid
Remove-Value 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved' $thumbClsid
Remove-Value 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved' $previewClsid

Write-Host "AMF QuickLook registry entries removed for this Windows user."
