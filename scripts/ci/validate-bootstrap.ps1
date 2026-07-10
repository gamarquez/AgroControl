$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\..\.."

$requiredPaths = @(
    "docker-compose.yml",
    ".env.example",
    ".env.development.example",
    ".env.test.example",
    ".env.staging.example",
    ".env.homologation.example",
    ".env.production.example",
    "apps",
    "packages",
    "infra",
    ".github/workflows/ci.yml",
    "scripts/ci/run-dotnet.ps1",
    "scripts/ci/run-web.ps1",
    "scripts/dev/up.ps1",
    "scripts/dev/down.ps1",
    "docs/operations/local-setup.md"
)

foreach ($relativePath in $requiredPaths) {
    $absolutePath = Join-Path $root $relativePath
    if (-not (Test-Path $absolutePath)) {
        throw "Missing required repository path: $relativePath"
    }
}

if (Get-Command docker -ErrorAction SilentlyContinue) {
    & docker compose -f (Join-Path $root "docker-compose.yml") config | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose config validation failed."
    }
}
else {
    Write-Host "Skipping docker compose validation: docker CLI not found."
}

Write-Host "Repository validation completed."
