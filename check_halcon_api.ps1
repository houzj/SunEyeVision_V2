param([string]$dllPath)

Add-Type -Path $dllPath
$asm = [System.Reflection.Assembly]::LoadFrom($dllPath)

Write-Host "=== HSmartWindowControl ==="
$type = $asm.GetType('HalconDotNet.HSmartWindowControl')
if ($type) {
    $type.GetMethods() | Where-Object { $_.IsPublic } | ForEach-Object { $_.Name } | Sort-Object -Unique
} else {
    Write-Host "NOT FOUND"
    Write-Host "Available types containing 'SmartWindow' or 'HWindow':"
    $asm.GetExportedTypes() | Where-Object { $_.Name -match 'SmartWindow|HWindow' } | ForEach-Object { $_.FullName }
}

Write-Host "=== HImage ==="
$hImg = $asm.GetType('HalconDotNet.HImage')
if ($hImg) {
    $props = $hImg.GetProperties() | Where-Object { $_.IsPublic }
    Write-Host "Properties:"
    $props | ForEach-Object { $_.Name }
} else {
    Write-Host "HImage NOT FOUND"
}

Write-Host "=== HOperatorSet DrawingObject ==="
$ops = $asm.GetType('HalconDotNet.HOperatorSet')
if ($ops) {
    $ops.GetMethods() | Where-Object { $_.Name -match 'CreateDrawingObject' -and $_.IsPublic } | ForEach-Object { 
        $name = $_.Name
        $params = $_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }
        Write-Host "$name($([string]::Join(', ', $params)))"
    }
}
