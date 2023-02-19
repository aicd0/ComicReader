Write-Output "pre-build start"
$scriptpath = $MyInvocation.MyCommand.Path
$dir = Split-Path $scriptpath
Set-Location $dir
python ./pre-build.py
Write-Output "pre-build end"
