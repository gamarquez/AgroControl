$ErrorActionPreference = "Stop"

param(
    [string]$ConnectionString = "",
    [string]$MigrationsPath = "infra/supabase/migrations"
)

function Resolve-ConnectionString {
    param([string]$ExplicitConnectionString)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitConnectionString)) {
        return $ExplicitConnectionString.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($env:SUPABASE_DB_CONNECTION_STRING)) {
        return $env:SUPABASE_DB_CONNECTION_STRING.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($env:POSTGRES_CONNECTION_STRING)) {
        return $env:POSTGRES_CONNECTION_STRING.Trim()
    }

    throw "Setea -ConnectionString o define SUPABASE_DB_CONNECTION_STRING / POSTGRES_CONNECTION_STRING."
}

function Invoke-PsqlFile {
    param(
        [Parameter(Mandatory = $true)][string]$EffectiveConnectionString,
        [Parameter(Mandatory = $true)][string]$SqlFilePath
    )

    if (Get-Command psql -ErrorAction SilentlyContinue) {
        & psql $EffectiveConnectionString -v ON_ERROR_STOP=1 -f $SqlFilePath
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }

        return
    }

    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "No se encontro psql ni docker para aplicar migraciones."
    }

    Get-Content -LiteralPath $SqlFilePath -Raw |
        & docker run --rm -i postgres:17-alpine psql $EffectiveConnectionString -v ON_ERROR_STOP=1 -f -

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$root = Resolve-Path "$PSScriptRoot\..\.."
$effectiveConnectionString = Resolve-ConnectionString -ExplicitConnectionString $ConnectionString
$resolvedMigrationsPath = Resolve-Path (Join-Path $root $MigrationsPath)
$migrationFiles = Get-ChildItem -Path $resolvedMigrationsPath -Filter "V*.sql" -File | Sort-Object Name

if (-not $migrationFiles) {
    throw "No se encontraron migraciones SQL en $resolvedMigrationsPath."
}

foreach ($migrationFile in $migrationFiles) {
    Write-Host "Applying migration $($migrationFile.Name)..."
    Invoke-PsqlFile -EffectiveConnectionString $effectiveConnectionString -SqlFilePath $migrationFile.FullName
}

Write-Host "Migraciones aplicadas correctamente."
