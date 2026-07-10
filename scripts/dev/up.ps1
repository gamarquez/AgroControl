$ErrorActionPreference = "Stop"
& docker compose up -d postgres

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
