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

El despliegue se configura como un `Web Service` conectado al repositorio. No se crea ni se sincroniza un Blueprint.

1. En Render Dashboard, seleccionar `New` > `Web Service`.
2. Elegir `Git Provider`, conectar GitHub si hace falta y seleccionar este repositorio.
3. Completar la configuracion del servicio:
   - `Name`: `agrocontrol-api` o el nombre definitivo elegido para la API.
   - `Region`: una region compatible con la latencia y ubicacion de los servicios externos usados por produccion.
   - `Branch`: `main`.
   - `Language`: `Docker`.
   - `Root Directory`: dejar vacio para usar la raiz del repositorio.
   - `Dockerfile Path`: `./Dockerfile`.
   - `Docker Command`: dejar vacio para usar el `ENTRYPOINT` del `Dockerfile`.
   - `Auto-Deploy`: `On Commit`, o `After CI Checks Pass` si Render tiene acceso a los checks del repositorio.
4. En `Advanced`, configurar `Health Check Path` con `/health/ready`.
5. En `Environment`, cargar manualmente todas las variables requeridas:

| Variable | Valor |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `PORT` | `10000` |
| `ASPNETCORE_URLS` | `http://0.0.0.0:10000` |
| `POSTGRES_CONNECTION_STRING` | Cadena de conexion PostgreSQL de produccion obtenida de Supabase. |
| `Auth__SigningKey` | Secreto aleatorio generado en Render; no reutilizar claves de desarrollo ni guardarlo en el repositorio. |
| `Auth__Issuer` | URL publica completa de la API, por ejemplo `https://agrocontrol-api.onrender.com`. |
| `Auth__Audience` | URL publica completa del frontend, por ejemplo `https://app.<dominio>`. |
| `Cors__AllowedOrigins` | Origen permitido del frontend, por ejemplo `https://app.<dominio>`, sin `/` final. Para varios origenes, separarlos con comas. |
| `AllowedHosts` | Host de la API sin protocolo, por ejemplo `agrocontrol-api.onrender.com`. |

Render asigna automaticamente `RENDER_EXTERNAL_URL` y `RENDER_EXTERNAL_HOSTNAME` al Web Service, pero las variables de configuracion de ASP.NET Core se cargan con sus valores concretos en el Dashboard. Si se agrega un dominio propio a la API, actualizar `Auth__Issuer` con la URL definitiva y agregar su host a `AllowedHosts`; los hosts de ASP.NET Core se separan con punto y coma.

6. Crear el Web Service y seguir el primer build desde `Events` o `Logs`.
7. Si se corrigio una variable despues del primer intento, usar `Save and deploy`; si el cambio debe participar tambien del build Docker, usar `Save, rebuild, and deploy`.
8. Confirmar que el deploy quede `Live` y que `/health/ready` responda correctamente antes de configurar el frontend contra la API.

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
