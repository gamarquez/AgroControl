# Despliegue inicial a produccion

## 1. Preparar Supabase

1. Crear un proyecto nuevo de Supabase para produccion.
2. Obtener la cadena de conexion PostgreSQL directa.
3. Definirla como `SUPABASE_DB_CONNECTION_STRING` o `POSTGRES_CONNECTION_STRING`.
4. Aplicar migraciones:

```powershell
./scripts/deploy/apply-supabase-migrations.ps1 -ConnectionString "<cadena>"
```

5. Crear el administrador inicial:

```powershell
./scripts/deploy/bootstrap-admin-production.ps1 -ConnectionString "<cadena>" -Email admin@miempresa.com -Password "ClaveSegura123!" -DisplayName "Administrador"
```

## 2. Desplegar API en Render

1. Crear un `Web Service` desde este repositorio.
2. Usar `render.yaml` como blueprint base.
3. Configurar variables:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ASPNETCORE_URLS=http://0.0.0.0:$PORT`
   - `AllowedHosts=api.<dominio>`
   - `Cors__AllowedOrigins=https://app.<dominio>`
   - `POSTGRES_CONNECTION_STRING` o `SUPABASE_DB_CONNECTION_STRING`
   - `Auth__SigningKey`
   - `Auth__Issuer=https://api.<dominio>`
   - `Auth__Audience=https://app.<dominio>`
4. Confirmar `healthCheckPath=/health/ready`.

## 3. Desplegar frontend en Vercel

1. Crear un proyecto con root directory `apps/web`.
2. Configurar variables en Vercel para `Preview` y `Production`:
   - `AGROCONTROL_API_BASE_URL=https://api.<dominio-o-api-preview>`
   - `NEXT_PUBLIC_API_BASE_URL=https://api.<dominio-o-api-preview>`
   - `NEXT_PUBLIC_API_HEALTH_PATH=/health`
   - `NEXT_TELEMETRY_DISABLED=1`
3. Mantener el contrato:
   - `AGROCONTROL_API_BASE_URL` es obligatoria para el servidor de Next.js y no debe depender de defaults locales.
   - `NEXT_PUBLIC_API_BASE_URL` es obligatoria para el chequeo de conectividad desde el navegador.
   - Si se usa una API distinta para previews, la API debe permitir CORS desde los dominios preview de Vercel que vayan a consumirla.
4. Publicar produccion desde `main`.

## 4. DNS recomendado

- `app.<dominio>` -> Vercel
- `api.<dominio>` -> Render

## 5. Smoke post-deploy

```powershell
$env:DEPLOY_API_BASE_URL="https://api.<dominio>"
$env:DEPLOY_APP_BASE_URL="https://app.<dominio>"
$env:DEPLOY_ADMIN_EMAIL="admin@miempresa.com"
$env:DEPLOY_ADMIN_PASSWORD="ClaveSegura123!"
node ./scripts/deploy/smoke-production.mjs
```

## 6. Validacion funcional minima

- `/health` responde `healthy`.
- `/health/ready` responde `healthy`.
- Login admin funciona por HTTPS.
- Catalogo lista productos desde el frontend.
- Stock lista productos desde el frontend.
- Se puede crear categoria, marca, producto y movimiento de stock sin errores de CORS.
