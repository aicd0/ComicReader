Write-Output "pre-build start"
$scriptpath = $MyInvocation.MyCommand.Path
$dir = Split-Path $scriptpath
Set-Location $dir
$result = python ./pre-build.py
Write-Output $result
Write-Output "pre-build end"
