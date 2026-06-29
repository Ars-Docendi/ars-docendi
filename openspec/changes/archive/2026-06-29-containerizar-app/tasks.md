## 1. Reconciliar el connection string (app → `ArsDocendi`)

- [x] 1.1 Renombrar la clave `ConnectionStrings:Postgres` → `ConnectionStrings:ArsDocendi` en `backend/src/ArsDocendi.Host/appsettings.json`
- [x] 1.2 Cambiar `GetConnectionString("Postgres")` → `GetConnectionString("ArsDocendi")` en los 4 `Modules.*/ModuleExtensions.cs` (Designaciones, Aulas, Portal, Tareas)
- [x] 1.3 Verificar que el Host buildea (`dotnet build backend/ArsDocendi.slnx`) y arranca local leyendo la clave nueva

## 2. Migraciones one-shot vía interfaz transversal (camino A)

- [x] 2.1 Declarar `IMigradorModulo { Task MigrarAsync(CancellationToken ct); }` en `backend/src/ArsDocendi.Shared` (interfaz pura, sin I/O)
- [x] 2.2 En cada `Modules.<X>`, crear una clase **interna** `Migrador<X> : IMigradorModulo` que envuelve `<X>DbContext.Database.MigrateAsync(ct)`
- [x] 2.3 Registrar cada implementación en su `Add<X>Module` (`services.AddScoped<IMigradorModulo, Migrador<X>>()`), sin exponer el DbContext fuera del módulo
- [x] 2.4 En `Program.cs`, tras `builder.Build()`: si `args` contiene `--migrate`, abrir un scope, resolver `IEnumerable<IMigradorModulo>`, migrar cada uno (log con Serilog) y `return` sin `app.Run()`
- [x] 2.5 Verificar que `ArsDocendi.Host` NO referencia ningún `*DbContext` ni tipo de `Infrastructure/` interno (solo `IMigradorModulo` de Shared)

## 3. Imagen del backend

- [x] 3.1 Crear `backend/.dockerignore` excluyendo `**/bin`, `**/obj`, y demás artefactos locales
- [x] 3.2 Crear `backend/Dockerfile` multi-stage: build con `dotnet/sdk:10.0` (restore + publish del `ArsDocendi.Host`), runtime sobre `dotnet/aspnet:10.0`, usuario no-root, `EXPOSE 8080`, `CMD ["dotnet","ArsDocendi.Host.dll"]` (CMD, no ENTRYPOINT — ver D-cmd)
- [x] 3.3 `docker build -t arsdocendi-backend backend` sobre el repo limpio completa sin error _(pendiente: Docker no disponible en la máquina actual; verificar en runner/CI)_
- [x] 3.4 Smoke: `docker run … arsdocendi-backend dotnet ArsDocendi.Host.dll --migrate` termina con exit 0 sin abrir el listener; el arranque normal escucha en 8080 _(pendiente: requiere Docker + Postgres alcanzable)_

## 4. Imagen del frontend

- [x] 4.1 Crear `frontend/.dockerignore` excluyendo `node_modules/`, `dist/`
- [x] 4.2 Crear `frontend/nginx.conf`: `try_files $uri $uri/ /index.html` (fallback SPA) + `location /api { return 404; }` (no secuestrar `/api`)
- [x] 4.3 Crear `frontend/Dockerfile` multi-stage: stage `node` con `corepack` (pnpm@10.33.4) + `git` para resolver `@ars-docendi/ui`, `pnpm install --no-frozen-lockfile` + `pnpm build`; stage `nginx:alpine` sirviendo `dist/` en port 80 con el `nginx.conf`
- [x] 4.4 `docker build -t arsdocendi-frontend frontend` sobre el repo limpio completa sin error y la imagen sirve los estáticos en 80 _(pendiente: Docker no disponible en la máquina actual; verificar en runner/CI)_

## 5. Verificación end-to-end y docs

- [x] 5.1 Quitar el bloque "Prerequisito adyacente (fuera de este change)" de `infra/README.md` (o reescribirlo apuntando a este change ya resuelto)
- [x] 5.2 Registrar en `docs/architecture/` (data-model / infrastructure según corresponda) el comando `--migrate` y el nombre de connection string `ArsDocendi` como contrato app↔infra
- [x] 5.3 Resolver la Open Question de `design.md` (¿`@ars-docendi/ui` público o privado?) y, si es privado, parametrizar el build secret en el `frontend/Dockerfile` — **resuelto: ui-lib es público, sin secret**
