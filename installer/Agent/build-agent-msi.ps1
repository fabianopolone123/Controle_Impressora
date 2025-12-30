$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..\\..")
$sourceDir = Join-Path $root "dist\\Agent"
$wxs = Join-Path $PSScriptRoot "PrintControl.Agent.wxs"
$outDir = Join-Path $root "dist\\Installer"
$msi = Join-Path $outDir "PrintControl.Agent.msi"
$intermediate = Join-Path $outDir "obj"

$exe = Join-Path $sourceDir "PrintControl.Agent.exe"
$config = Join-Path $sourceDir "appsettings.json"

if (!(Test-Path $exe)) {
  throw "Missing $exe. Publish the agent to dist\\Agent first."
}

if (!(Test-Path $config)) {
  throw "Missing $config. Ensure appsettings.json is in dist\\Agent."
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
New-Item -ItemType Directory -Force -Path $intermediate | Out-Null

$wixExe = Join-Path $env:USERPROFILE ".dotnet\\tools\\wix.exe"
if (!(Test-Path $wixExe)) {
  dotnet tool install --global wix
}

& $wixExe build `
  -arch x64 `
  -d SourceDir="$sourceDir" `
  -intermediateFolder "$intermediate" `
  -out "$msi" `
  "$wxs"

if ($LASTEXITCODE -ne 0) {
  throw "MSI build failed."
}

Write-Host "MSI created: $msi"
