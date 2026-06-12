## Context

`ephemeral-environments` dejó toda la plataforma de deploy escrita y a punto de archivarse, pero declaró explícitamente como Non-Goal el empaquetado de la app. El `compose.base.yml`, los workflows (`deploy-prod`, `deploy-staging`, `pr-env-deploy`) y `spin-up.sh` ya están escritos **esperando** dos cosas que no existen:

1. Imágenes `arsdocendi-backend` (port 8080) y `arsdocendi-frontend` (port 80), buildeadas con `docker build … backend` y `docker build … frontend` (contexto = cada carpeta).
2. Un backend que ejecute migraciones en modo one-shot: `spin-up.sh` corre `run --rm backend ${COMANDO_MIGRACIONES:-dotnet ArsDocendi.Host.dll --migrate}`.

Estado actual del backend: EF Core ya cableado (`Npgsql.EntityFrameworkCore.PostgreSQL` + `Microsoft.EntityFrameworkCore.Design` en el Host), **4 DbContext** internos (uno por módulo, `HasDefaultSchema` → un schema por módulo), carpetas `Migrations/` con solo `.gitkeep`. `Program.cs` solo levanta el web server; no parsea `--migrate`. Los 4 `ModuleExtensions.cs` leen `GetConnectionString("Postgres")`, pero la infra inyecta `ConnectionStrings__ArsDocendi` → mismatch que devolvería null en runtime.

Restricción dura: **no tocar la infra ya escrita** (compose, scripts, workflows). Las piezas nuevas deben encajar en los contratos que esos archivos ya fijaron.

## Goals / Non-Goals

**Goals:**

- Que `docker build … backend` y `docker build … frontend` produzcan imágenes operables sobre el repo limpio.
- Que el backend soporte `--migrate` como proceso one-shot idempotente, respetando la frontera de módulos (invariante #1).
- Reconciliar el nombre del connection string de un solo lado (la app → `ArsDocendi`), sin tocar la infra.

**Non-Goals:**

- Definir entidades de dominio o generar migraciones EF reales (las carpetas siguen con `.gitkeep`; `--migrate` queda operativo y aplicará migraciones cuando cada módulo gane entidades).
- Tocar `compose.base.yml`, `spin-up.sh`, los workflows o cualquier artefacto de `ephemeral-environments`.
- Autenticación de registry para `docker push` (es responsabilidad de CI / runbook, ya cubierta por `ephemeral-environments`).
- Optimización fina de tamaño de imagen o cache de capas más allá del multi-stage básico.

## Decisions

### D-mig — Migración vía interfaz transversal en `ArsDocendi.Shared` (camino A)

Cada módulo expone su rutina de migración a través de una interfaz pura, **sin** que el Host conozca los `*DbContext` internos.

- `ArsDocendi.Shared` declara `IMigradorModulo { Task MigrarAsync(CancellationToken ct); }` — declaración pura, sin I/O (respeta invariante #4: Shared puro).
- Cada `Modules.<X>` registra en su `Add<X>Module` una implementación **interna** que envuelve su DbContext:
  ```csharp
  internal sealed class MigradorAulas(AulasDbContext db) : IMigradorModulo
  {
      public Task MigrarAsync(CancellationToken ct) => db.Database.MigrateAsync(ct);
  }
  // services.AddScoped<IMigradorModulo, MigradorAulas>();
  ```
- El Host, al ver `--migrate`, resuelve `IEnumerable<IMigradorModulo>` del DI y migra cada uno. No referencia ningún `*DbContext` ni `Infrastructure/` interno.

**Por qué la interfaz va en Shared y no en cada `*.Contracts`:** es un contrato transversal del composition root, idéntico para los 4 módulos. Ponerlo en Shared evita 4 interfaces duplicadas y un `IEnumerable` heterogéneo. El Host ya depende de Shared (`AddArsDocendiShared`). Las implementaciones (lo que hace I/O) viven **dentro** del módulo, internas. Alternativa considerada: interfaz por módulo en `Modules.<X>.Contracts` (lo que sugería la propuesta) — rechazada por duplicación; la frontera de módulos se respeta igual porque la implementación sigue siendo interna al módulo.

### D-cmd — La imagen del backend usa `CMD`, no `ENTRYPOINT`

`spin-up.sh` pasa el comando **completo** (`dotnet ArsDocendi.Host.dll --migrate`) como override. En `docker compose run SERVICE COMMAND`, el COMMAND reemplaza el `CMD` de la imagen pero **se concatena** a un `ENTRYPOINT`. Si la imagen usara `ENTRYPOINT ["dotnet","ArsDocendi.Host.dll"]`, el override daría `dotnet ArsDocendi.Host.dll dotnet ArsDocendi.Host.dll --migrate` (roto). Por eso la imagen usa `CMD ["dotnet","ArsDocendi.Host.dll"]`: el arranque normal corre el CMD; el spin-up lo reemplaza entero. Mantiene la infra intacta (no hay que cambiar `COMANDO_MIGRACIONES`).

### D-flujo — `--migrate` buildea el host pero no abre el listener

```csharp
var app = builder.Build();
if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    foreach (var migrador in scope.ServiceProvider.GetServices<IMigradorModulo>())
        await migrador.MigrarAsync(CancellationToken.None);
    return; // exit 0, sin app.Run()
}
// … pipeline normal + app.Run()
```

`builder.Build()` no arranca hosted services (eso lo hace `Run()`), así que construir el host completo es seguro y reusa el mismo DI/Serilog. Cada migración se loguea con Serilog (nunca `Console.WriteLine`). El orden entre módulos es irrelevante: cada uno migra su propio schema, sin FKs cross-schema (la frontera modular lo garantiza). Idempotencia la da EF (`Database.Migrate()` no reaplica migraciones ya presentes).

### D-cstr — La app se alinea al nombre `ArsDocendi`

Los 4 `ModuleExtensions.cs` pasan a `GetConnectionString("ArsDocendi")` y `appsettings.json` renombra la clave `Postgres` → `ArsDocendi`. Decidido con el usuario: el nombre describe la app (no el motor), y deja intacto `ephemeral-environments` (que ya inyecta `ConnectionStrings__ArsDocendi`). Alternativa: cambiar la infra a `__Postgres` — rechazada (reabriría un change casi archivado y "Postgres" nombra el motor, no el ambiente).

### D-front — nginx estático con fallback SPA y `/api` fuera de juego

Stage 1 (`node`): `corepack` habilita `pnpm@10.33.4` (campo `packageManager`); `apk add git` para resolver `@ars-docendi/ui` (`github:`); `pnpm install --frozen-lockfile` + `pnpm build`. Stage 2 (`nginx:alpine`): copia `dist/` a `/usr/share/nginx/html` + un `nginx.conf` con `try_files $uri $uri/ /index.html` (fallback react-router) y un `location /api { return 404; }` explícito (defense-in-depth: aunque Traefik rutea `/api` al backend con prioridad mayor antes de llegar acá, el contenedor nunca sirve `index.html` para `/api`). nginx stock escucha en 80 como pide la label de Traefik.

## Risks / Trade-offs

- **`@ars-docendi/ui` podría ser un repo privado** → si lo es, `pnpm install` dentro del build necesita un token de GitHub inyectado como build secret. Mitigación: ver Open Questions; si es público, no hace falta nada. No se hardcodea ningún token en el Dockerfile.
- **`bin/`/`obj/` versionados sucios inflan el contexto de build** → `.dockerignore` en `backend/` y `frontend/` los excluye; es la pieza que hace el build determinístico y reproducible.
- **`--migrate` hoy es no-op** (no hay migraciones reales) → aceptado: el objetivo es que el _comando exista y salga 0_. El smoke test valida exit 0; la creación de schema se testeará cuando los módulos ganen entidades.
- **nginx del frontend corre master como root** → aceptado: sirve solo estáticos, sin secrets; usar `nginx-unprivileged` obligaría a escuchar en 8080 y desalinearía la label de Traefik (port 80). El backend sí corre no-root.
- **Disponibilidad de tags de imagen .NET 10** → se fija la familia `10.0` (sdk y aspnet) coherente con `global.json` (`10.0.201`). Si el tag exacto no estuviera publicado, ajustar al disponible de la misma minor.

## Migration Plan

1. Agregar archivos nuevos (Dockerfiles, `.dockerignore`, `nginx.conf`, `IMigradorModulo` + 4 implementaciones) y el branch `--migrate`. No afecta el runtime actual.
2. Reconciliar el connection string (4 `ModuleExtensions.cs` + `appsettings.json`). Validar build local + arranque local con `ConnectionStrings:ArsDocendi`.
3. `docker build` local de ambas imágenes para verificar que el contexto y los stages funcionan sobre el repo limpio.
4. **Rollback**: borrar los archivos nuevos y revertir el branch `--migrate`/los renombres. El web server normal y el `appsettings.json` previo quedan intactos. Prod no se ve afectado: sigue en el deploy viejo hasta que se migre el DNS (paso 5 del Migration Plan de `ephemeral-environments`).

## Open Questions

- ~~**¿`@ars-docendi/ui` (`github:Ars-Docendi/ui-lib`) es público o privado?**~~ **Resuelto**: el repo es **público** (la API de GitHub responde 200 sin auth). El `frontend/Dockerfile` solo necesita `git` instalado para resolver la dependencia; **no** hace falta build secret ni token.
