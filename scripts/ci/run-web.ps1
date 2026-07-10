param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("lint", "build", "test")]
    [string]$Command
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\..\.."
$webPath = Resolve-Path "$root\apps\web" -ErrorAction Stop
$packageJson = Join-Path $webPath "package.json"

if (-not (Test-Path $packageJson)) {
    Write-Host "Skipping web ${Command}: apps/web/package.json not found."
    exit 0
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm is required."
}

$workspaceName = $null
try {
    $workspaceName = (Get-Content -Raw $packageJson | ConvertFrom-Json).name
}
catch {
    $workspaceName = $null
}

if ([string]::IsNullOrWhiteSpace($workspaceName)) {
    $arguments = @("--prefix", $webPath, "run", $Command, "--if-present")
}
else {
    $arguments = @("run", "--workspace", $workspaceName, $Command, "--if-present")
}

Write-Host "Running: npm $($arguments -join ' ')"
Push-Location $root
try {
    & npm @arguments
}
finally {
    Pop-Location
}

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
