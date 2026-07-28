param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,

    [Parameter(Mandatory = $true)]
    [string]$KKVRRoot,

    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "KK_VR_CameraSync.csproj"
$msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue

if ($null -eq $msbuild) {
    throw "MSBuild was not found. Install Visual Studio Build Tools with the .NET Framework 3.5 targeting pack."
}

& $msbuild.Source $project `
    "/p:Configuration=$Configuration" `
    "/p:GameRoot=$GameRoot" `
    "/p:KKVRRoot=$KKVRRoot"

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

$output = Join-Path $PSScriptRoot "bin\$Configuration\net35\KK_VR_CameraSync.dll"
Write-Host "Built: $output"
