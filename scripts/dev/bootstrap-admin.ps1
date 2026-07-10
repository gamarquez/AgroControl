$ErrorActionPreference = "Stop"

param(
    [string]$Email = "admin@agrocontrol.local",
    [string]$Password = "ChangeMe123!",
    [string]$DisplayName = "Administrador AgroControl",
    [string]$PostgresUser = "agrocontrol",
    [string]$PostgresDatabase = "agrocontrol"
)

function New-PasswordHash {
    param([Parameter(Mandatory = $true)][string]$PlainPassword)

    $salt = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
    $key = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2(
        $PlainPassword,
        $salt,
        100000,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        32)

    return @(
        "PBKDF2-SHA256"
        "100000"
        [Convert]::ToBase64String($salt)
        [Convert]::ToBase64String($key)
    ) -join '$'
}

function Escape-SqlLiteral {
    param([Parameter(Mandatory = $true)][string]$Value)
    return $Value.Replace("'", "''")
}

$passwordHash = New-PasswordHash -PlainPassword $Password
$escapedEmail = Escape-SqlLiteral -Value $Email.Trim().ToLowerInvariant()
$escapedDisplayName = Escape-SqlLiteral -Value $DisplayName.Trim()
$escapedPasswordHash = Escape-SqlLiteral -Value $passwordHash

$sql = @"
with target_org as (
    select organization_id
    from app.organizations
    order by created_at asc
    limit 1
),
upserted_user as (
    insert into app.users (
        user_id,
        organization_id,
        email,
        display_name,
        password_hash,
        is_active,
        is_locked,
        must_change_password)
    select
        gen_random_uuid(),
        organization_id,
        '$escapedEmail',
        '$escapedDisplayName',
        '$escapedPasswordHash',
        true,
        false,
        false
    from target_org
    on conflict (organization_id, email) do update
    set display_name = excluded.display_name,
        password_hash = excluded.password_hash,
        is_active = true,
        is_locked = false,
        must_change_password = false
    returning user_id, organization_id
)
delete from app.user_roles
where user_id = (select user_id from upserted_user);

insert into app.user_roles (user_id, role_id)
select u.user_id, r.role_id
from upserted_user u
join app.roles r
    on r.organization_id = u.organization_id
   and r.code = 'administrator'
on conflict (user_id, role_id) do nothing;
"@

$tempFile = Join-Path $env:TEMP "agrocontrol-bootstrap-admin.sql"
Set-Content -LiteralPath $tempFile -Value $sql -Encoding UTF8

try {
    & docker compose exec -T postgres psql -v ON_ERROR_STOP=1 -U $PostgresUser -d $PostgresDatabase -f - < $tempFile

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    Write-Host "Bootstrap admin listo para $Email"
} finally {
    if (Test-Path -LiteralPath $tempFile) {
        Remove-Item -LiteralPath $tempFile -Force
    }
}
