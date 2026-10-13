# Build and run the legacy eShopLegacyMVC app on http://localhost:5050
$ErrorActionPreference = "Stop"

$vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath
& "$vs\Common7\Tools\Launch-VsDevShell.ps1" -SkipAutomaticLocation

Set-Location "$PSScriptRoot\legacy\eShopLegacyMVCSolution"
msbuild eShopLegacyMVC.sln -t:Restore -p:RestorePackagesConfig=true -v:minimal
msbuild eShopLegacyMVC.sln -p:Configuration=Debug -v:minimal

$web = (Get-ChildItem -Recurse -Filter *.csproj | Where-Object { Test-Path (Join-Path $_.DirectoryName "Web.config") } | Select-Object -First 1).DirectoryName
& "C:\Program Files\IIS Express\iisexpress.exe" /path:"$web" /port:5050