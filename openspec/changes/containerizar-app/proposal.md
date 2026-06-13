## Why

La plataforma de ambientes efímeros (`ephemeral-environments`) deja todos los artefactos de infra listos, pero declara como Non-Goal el empaquetado de la app. Hoy **no existen** `frontend/Dockerfile` ni `backend/Dockerfile`, y el backend no soporta el comando de migraciones que `spin-up.sh` invoca. Sin estas piezas, los workflows (`deploy-prod`, `deploy-staging`, `pr-env-deploy`) fallan en el primer `docker build`, y ningún ambiente —ni prod, ni staging, ni pr-N— puede materializarse. Este change cierra ese prerequisito adyacente para que la infra ya construida sea operable de punta a punta.

## What Changes

- **Nuevo `backend/Dockerfile`** (multi-stage: `sdk:10.0` para build → `aspnet:10.0` runtime, usuario no-root, `EXPOSE 8080`) + `backend/.dockerignore`. Produce la imagen `arsdocendi-backend` que consumen los workflows y `compose.base.yml`.
- **Nuevo `frontend/Dockerfile`** (multi-stage: `node` para `pnpm build` → `nginx:alpine` sirviendo `dist/` en port 80) + `frontend/.dockerignore` + `frontend/nginx.conf` con fallback SPA para react-router. El nginx del frontend solo sirve estáticos; **no** proxea `/api` (eso lo rutea Traefik).
- **Soporte de migraciones en el backend**: `Program.cs` reconoce el argumento `--migrate`, aplica las migraciones de los 4 módulos y termina con exit code 0 sin levantar el web server. Cada módulo expone su propia rutina de migración vía su `*.Contracts` (camino A: el Host **no** referencia los `*DbContext` internos, respetando el invariante #1).
- **Reconciliación del connection string**: los 4 `ModuleExtensions.cs` + `appsettings.json` pasan a usar el nombre `ArsDocendi` (hoy leen `Postgres`), alineándose con lo que `compose.base.yml`/`spin-up.sh` ya inyectan (`ConnectionStrings__ArsDocendi`). Sin esto, el backend deployado leería una connection string nula.

No se modifica ningún workflow, script de infra ni `compose.base.yml`: ya están escritos esperando estas piezas. No se agregan entidades ni migraciones de dominio (las carpetas `Migrations/` siguen con `.gitkeep`); el comando `--migrate` queda operativo y aplicará migraciones a medida que cada módulo gane entidades.

## Capabilities

### New Capabilities

- `empaquetado-imagenes-app`: define el contrato de las imágenes de contenedor de la app (backend y frontend) que la plataforma de ambientes consume — qué expone cada imagen, en qué puerto, cómo se sirve el frontend, y cómo el backend ejecuta migraciones de forma idempotente en modo one-shot.

### Modified Capabilities

<!-- Ninguna. `plataforma-ambientes-efimeros` y `pipeline-deploy-ci` (de ephemeral-environments) ya asumen estas imágenes como entrada; este change las provee sin alterar sus requisitos. -->

## Impact

- **Código backend**: `backend/src/ArsDocendi.Host/Program.cs` (branch `--migrate`), `appsettings.json` (nombre del connection string), los 4 `Modules.*/ModuleExtensions.cs` (`GetConnectionString("ArsDocendi")`). Cada `Modules.*.Contracts` gana una interfaz/token de migración (DTOs/interfaces, sin lógica → respeta invariante #4).
- **Grafo de dependencias**: sin edges nuevos cross-module. El Host ya referencia los 4 módulos; la rutina de migración se expone por Contracts (que el Host ya consume). No se introducen ciclos.
- **Build/CI**: nuevos `Dockerfile` y `.dockerignore` en `frontend/` y `backend/`; los workflows existentes empiezan a buildear con éxito. `.dockerignore` es crítico porque el repo versiona `bin/`/`obj/` sucios que inflarían el contexto y romperían el build determinístico.
- **Dependencia externa de build**: el `frontend/Dockerfile` necesita git + red durante `pnpm install` para resolver `@ars-docendi/ui` (dependencia `github:`).
- **Rollback**: agregar archivos nuevos y un branch de arranque no afecta el runtime actual (prod sigue en el deploy viejo hasta que se migre el DNS, ver Migration Plan de `ephemeral-environments`). Revertir el change = borrar los Dockerfiles y el branch `--migrate`; el web server normal queda intacto.
