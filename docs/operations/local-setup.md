# Entorno local

## Requisitos

- Docker Desktop con `docker compose`.
- .NET SDK 10.
- Node.js 24 y npm.

## Variables de entorno

1. Copiar `.env.example` a `.env`.
2. Ajustar los valores locales si hace falta.
3. Para otros entornos, usar el archivo `*.example` correspondiente como base:
   - `.env.test.example`
   - `.env.staging.example`
   - `.env.homologation.example`
   - `.env.production.example`

## Base de datos local

Levantar PostgreSQL local:

```powershell
./scripts/dev/up.ps1
```

Detener servicios locales:

```powershell
./scripts/dev/down.ps1
```

La base local expone `localhost:${POSTGRES_PORT}` y queda pensada para desarrollo y tests de integracion locales. Produccion, staging y homologacion deben usar credenciales separadas y fuera del repositorio.

## Backend

El backend existente se valida con:

```powershell
./scripts/ci/run-dotnet.ps1 restore
./scripts/ci/run-dotnet.ps1 build
./scripts/ci/run-dotnet.ps1 test
```

Los scripts de .NET usan `NuGet.Config` del repo y caches locales en `.appdata/` y `.nuget/` para no depender del perfil global de la maquina.

## Frontend

Con `apps/web` ya presente, los comandos esperados son:

```powershell
npm install
./scripts/ci/run-web.ps1 lint
./scripts/ci/run-web.ps1 build
./scripts/ci/run-web.ps1 test
```

## CI

El workflow `.github/workflows/ci.yml` valida:

- archivos minimos del repositorio,
- sintaxis de `docker-compose.yml`,
- restore/build/test de .NET,
- lint/build/test del frontend desde el workspace raiz.
