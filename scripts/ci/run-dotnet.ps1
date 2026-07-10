param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("restore", "build", "test")]
    [string]$Command
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\..\.."
$nugetConfig = Join-Path $root "NuGet.Config"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet CLI is required."
}

$solution = Join-Path $root "AgroControl.slnx"

if (Test-Path $solution) {
    $target = $solution
}
else {
    $projects = Get-ChildItem -Path (Join-Path $root "apps\api") -Recurse -Filter *.csproj -File -ErrorAction SilentlyContinue
    if (-not $projects) {
        Write-Host "Skipping .NET ${Command}: no solution or project found under apps/api."
        exit 0
    }

    $target = $projects[0].FullName
}

$arguments = @($Command, $target)

if ($Command -eq "restore" -and (Test-Path $nugetConfig)) {
    $localAppData = Join-Path $root ".appdata"
    $localNugetPackages = Join-Path $root ".nuget\packages"

    New-Item -ItemType Directory -Force -Path $localAppData | Out-Null
    New-Item -ItemType Directory -Force -Path $localNugetPackages | Out-Null

    $env:APPDATA = $localAppData
    $env:NUGET_PACKAGES = $localNugetPackages
    $arguments += "--configfile"
    $arguments += $nugetConfig
}

if ($Command -eq "build") {
    $arguments += "--no-restore"
}

if ($Command -eq "test") {
    $arguments += "--no-build"
    $arguments += "--verbosity"
    $arguments += "normal"
}

Write-Host "Running: dotnet $($arguments -join ' ')"
& dotnet @arguments

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
