$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\..\.."
$nugetConfig = Join-Path $root "NuGet.Config"
$apiProject = Join-Path $root "apps/api/src/AgroControl.Api/AgroControl.Api.csproj"
$outputDir = Join-Path $root "out"
$localAppData = Join-Path $root ".appdata"
$localNugetPackages = Join-Path $root ".nuget/packages"

New-Item -ItemType Directory -Force -Path $localAppData | Out-Null
New-Item -ItemType Directory -Force -Path $localNugetPackages | Out-Null

$env:APPDATA = $localAppData
$env:NUGET_PACKAGES = $localNugetPackages

Push-Location $root
try {
    & dotnet restore $apiProject --configfile $nugetConfig
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    & dotnet publish $apiProject -c Release -o $outputDir --no-restore
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
