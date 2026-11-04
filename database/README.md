# SQL versionado y baseline

El DDL bajo `database/<schema>/` es la fuente autorizada del modelo. EF Core
registra su aplicación; no genera las tablas de negocio. Los recursos se embeben
en cada assembly y no se leen del filesystem en runtime.

## Corte de alpha para bases nuevas

| Orden | Contexto       | Migración                               | Historial                             |
| ----- | -------------- | --------------------------------------- | ------------------------------------- |
| 1     | Identity/Audit | `20261005000000_BaselineIdentityYAudit` | `identity.__EFMigrationsHistory`      |
| 2     | Storage        | `20261005000100_BaselineStorage`        | `storage.__EFMigrationsHistory`       |
| 3     | Designaciones  | `20261005000200_BaselineDesignaciones`  | `designaciones.__EFMigrationsHistory` |
| 4     | Portal         | `20261005000300_BaselinePortal`         | `portal.__EFMigrationsHistory`        |

Aulas y Tareas no tienen baseline artificial: incorporan sus migraciones SQL en
`aulas.__EFMigrationsHistory` y `tareas.__EFMigrationsHistory`. Tareas se ejecuta después de Identity,
Storage, Designaciones, Aulas y Portal; sus timestamps se ordenan dentro de su
propio contexto, no globalmente. Antes del corte,
Designaciones/Portal/Aulas/Tareas usaban el historial compartido de `public`;
el cambio de ubicación es deliberado y **no convierte bases anteriores**.
El preflight debe rechazar historia alpha, IDs desconocidos/discontinuos y schemas
gestionados sin historial reconocido antes de cualquier escritura. No hacer
`stamp`, `DROP` ni resets automáticos sobre esos destinos.

## Verificación de migraciones

`MigracionesRuntimeTests` verifica instalación nueva, preview sin escrituras, upgrade desde el baseline con datos y reejecución sin nuevas filas de auditoría. CI complementa con la validación Git contra el SHA base, el build Release y `--validar-recursos`. Si `dotnet test` descubre cero tests, ejecutar el assembly Release con `dotnet vstest` y verificar los totales del TRX.

## Orden de recursos

La lista **única y ordenada** está en `[RecursosMigracionSql(...)]` de cada
baseline. `Up` llama `migrationBuilder.AplicarRecursosSql(typeof(Baseline...))`.
Los nombres numéricos ayudan a leer, pero no sustituyen esa metadata.

- Identity: primero `users` y su PK, después infraestructura Audit; siguen
  autorización, catálogo académico, personas/ámbitos, integridad, catálogos de
  sistema y enganche diferido de auditoría.
- Storage: metadata final de archivos y enganche auditado.
- Designaciones: catálogos, pedidos/períodos, documentación/historial,
  vigencias con `btree_gist`, idempotencia, funciones/FKs/triggers, datos de
  catálogo y enganche auditado.
- Portal: perfil/contactos/CV, trayectoria, proyectos/habilidades, integridad
  y enganche auditado.

Los enganches usan `audit.attach` explícitamente. Las PK no convencionales
conservan `rol_id` para `identity.rol_permisos` y `perfil_id` para
`portal.docente_habilidades`. Los catálogos se insertan antes del enganche,
como en la instalación anterior; no producen filas de auditoría de usuarios.

## Modelo preservado

Se conserva el estado final real de `61b2686`: materia canónica y carrera
directas en ámbitos y negocio, planes/materias-plan informativos, textos
históricos de dedicación, snapshots, horas complementarias, constraints,
índices, secuencias, funciones y triggers. No se recrean los modelos transitorios
ni se ejecutan backfills de alpha. Catálogos con UUID estables: roles, permisos,
rol-permisos, cargos y dedicaciones; `created_at` conserva su semántica `now()`.

La captura real confirmó que `archivo_id` es una referencia lógica **sin FK**
a `storage.archivos` en pedidos adjuntos, CV y documentos de proyecto. También
confirmó que `designaciones.designaciones.materia_id` no tiene FK simple en el
estado final anterior (se retiró su FK compuesta en alpha). Este squash no
inventa ni elimina constraints para corregir esa discrepancia: conserva el
schema comprobado. Agregar una FK exige otro cambio/migración con su validación.

## Diagnóstico previo a garantías nuevas

```bash
dotnet ArsDocendi.Host.dll --diagnosticar-integridad
```

Ejecutar con la conexión del destino inyectada por el canal seguro y escritores
conocidos detenidos. Este modo no levanta listener ni aplica migraciones. Devuelve
JSON con ambiente, release, histories observados y hallazgos de referencias,
normalización y jerarquía, limitados a categorías/conteos/IDs técnicos. Exit 1
ante incompatibilidad o falta de acceso; jamás interpretar un lector faltante
como cero referencias. Las lecturas locales son transacciones de solo lectura,
no una única fotografía global de todos los módulos ni una detección de drift.

Ante incompatibilidad: detener publicación, conservar evidencia y backup,
solicitar saneamiento manual específico y volver a diagnosticar. No fusionar
cuentas, inventar horas, reasignar huérfanos, purgar eventos ni resetear destinos
como parte del upgrade. La migración incremental debe validar también sus
invariantes al aplicar, para no convertir este diagnóstico en garantía TOCTOU.

## Autoría posterior

Nunca editar un SQL o wrapper ya aplicado. Crear una **migración incremental
nueva**, declarar sus recursos ordenados y mantener actualizado el snapshot del
contexto. El scaffolder/check de integridad del repositorio asiste esa autoría;
el inventario protegido se establece con este corte revisado. No agregar un
archivo a un baseline ya publicado ni volver a generar el baseline.

### Crear SQL y wrapper juntos

Desde la raíz (Node 24, sin dependencias adicionales):

```bash
node scripts/migrations/scaffold.mjs Identity AgregarIndice --dry-run
node scripts/migrations/scaffold.mjs Identity AgregarIndice
node --test scripts/tests/migrations.test.mjs
node scripts/migrations/validate.mjs
# Después del build, verifica recursos reales del assembly, sin DB:
dotnet run --project backend/src/ArsDocendi.Host --no-build -c Release -- --validar-recursos
```

Contextos admitidos: `Identity`, `Storage`, `Designaciones`, `Portal`, `Aulas`
y `Tareas`. El nombre debe ser PascalCase ASCII (hasta 80 caracteres, sin
separadores). La salida JSON describe SQL y wrapper, sus contenidos y el ID
`YYYYMMDDHHMMSS_NombreContexto` en UTC. El sufijo de contexto evita identidades
compartidas en un mismo segundo. `--dry-run` valida sin escribir. `--root <ruta>`
permite un workspace desechable; no conecta a bases ni aplica migraciones.
Colisiones de archivo o clase se rechazan sin sobrescribir.

Completar el SQL, revisar dependencia/orden y actualizar el modelo/snapshot EF
cuando corresponda. El wrapper tiene `[DbContext(typeof(...))]`,
`[Migration("...")]` y `[RecursosMigracionSql("schema/archivo.sql", ...)]`;
`Up` consume **esa misma metadata** con `AplicarRecursosSql(typeof(Clase))`.
No usar `dotnet ef migrations add` como generador de DDL paralelo. Si Aulas/Tareas
incorporan su primer schema, incluir explícitamente sus SQL como recursos embebidos
en su proyecto; el scaffolder no modifica `.csproj` ni crea baselines vacíos.

### Validación y protección histórica

`validate.mjs` valida declaraciones literales de los wrappers, contexto explícito,
IDs/timestamps únicos, rutas seguras, recursos existentes/únicos, consumidor
`Up` y SQL huérfano. Su parser ignora comentarios y no evalúa C#. La comprobación
de recursos **realmente embebidos** la hace `--validar-recursos` después del
build; el check de texto no sustituye ese paso ni detecta drift manual de la DB.

```bash
# SHA completo e inmutable obtenido de la rama base confiable (no HEAD del PR):
BASE_REF=$(git rev-parse origin/develop)
node scripts/migrations/validate.mjs --base-ref "$BASE_REF"
```

Sin `--base-ref`, el comando sólo coteja el manifiesto local y declara
`proteccionGit: false`; **no es la protección de CI**. En CI se toma el SHA base
del evento de PR o el SHA anterior del push, con checkout de historia completa.
Los SQL y wrappers históricos se comparan con bytes de ese commit confiable,
no con hashes editables por el PR. También quedan protegidas las migraciones
incrementales ya publicadas aunque no se regenere el manifiesto global.
Las adiciones deben tener un ID posterior al último publicado **del mismo
contexto**: intercalar un ID rompe el prefijo que valida el preflight en las
bases existentes. El check con `--base-ref` también rechaza esa intercalación;
el directorio del recurso SQL no determina el historial (por ejemplo, `audit/`
puede pertenecer a Identity).
Los `*ModelSnapshot.cs` representan el estado EF actual y pueden evolucionar;
no son operaciones históricas inmutables. Los `.Designer.cs` históricos sí se
protegen. Editar/eliminar un archivo protegido, retirar su entrada o cambiar su
hash/clasificación histórica falla con exit 1. Agregar una migración válida se
permite sin una nueva versión global.

### Manifiesto protegido

`database/migraciones-protegidas.json` (formato 1) guarda los hashes SHA-256 de SQL y wrappers históricos y sus `clasificados`. El corte inicial ya se ejecutó y no se repite: el CLI rechaza `--corte-revisado` cuando la base contiene manifiesto, y `manifest.mjs` rechaza archivos alterados o eliminados antes de regenerar (no hay `--force`). Para auditarlo: `node scripts/migrations/manifest.mjs --dry-run` y `node scripts/migrations/validate.mjs`.

Los SQL sin consumidor EF sólo se admiten con una excepción de ruta **exacta** y motivo, nunca con globs, pasada como JSON de clasificaciones (`manifest.mjs --clasificaciones <archivo>`):

```json
{
  "identity/soporte_bootstrap.sql": "Soporte administrativo de bootstrap; no se aplica desde migraciones EF."
}
```

El ejemplo no autoriza crear ese recurso: su existencia y la clasificación se revisan juntas. Se rechazan excepciones vacías, inexistentes, duplicadas con un consumidor o modificaciones a clasificaciones históricas. Si falta el commit base o la historia Git, CI falla cerrada.

```bash
# Aplicación one-shot, sin listener HTTP
dotnet run --project backend/src/ArsDocendi.Host -- --migrate
```

La reejecución sólo es no-op para historia compatible. `Down` del baseline falla
explícitamente: recuperar un backup en un destino aislado y una versión
compatible, no borrar el schema para recuperar una instancia persistente.

## Integración de incrementales del Asistente

La rama del asistente traía migraciones con IDs anteriores al baseline consolidado
(agosto–septiembre de 2026). Como nunca se aplicaron sobre una base del corte de
alpha, se integraron como incrementales **posteriores** al último ID de develop,
con el nombre que genera `scaffold.mjs` (`<id>_<Nombre><Contexto>`) y el SQL
adaptado al modelo académico canónico:

| Contexto      | IDs                               | Contenido                                                                                                                         |
| ------------- | --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| Identity      | `20261103000000`–`20261103000800` | Permisos del asistente, funciones de alcance del actor, comentarios para el prompt, `unaccent`                                    |
| Identity      | `20261103000900`                  | `audit.safe_snapshot` conserva `deleted_at` de `identity.user_roles` (la versión publicada nombraba `revocado_at`, que no existe) |
| Identity      | `20261103001000`                  | Permiso de revisión de calidad del asistente, sin asignaciones iniciales                                                          |
| Designaciones | `20261103001100`–`20261103001400` | RLS del asistente, comentarios y la función `identity.asistente_alcanza_a` (lee `designaciones`)                                  |
| Portal        | `20261020001500`–`20261020001700` | RLS del asistente, comentarios y RLS por ámbito                                                                                   |

El 2026-11-03 el autor autorizó excepcionalmente renumerar las migraciones
exclusivas del PR 40, aplicadas sólo en su preview, y reconstruir explícitamente
`pr-40`. El merge de develop había intercalado migraciones en Identity y
Designaciones: los 15 incrementales conservados pasan a los IDs de noviembre
de la tabla, posteriores a `20261102000906` de Identity y `20261028033141` de
Designaciones. Esto incluye `AuditBusquedaIdentity`: su SQL vive en `audit/`,
pero se registra en Identity. Portal conserva sus IDs.

Se retira `20261020000600_PermisoUsuariosVerDecanatoIdentity`: develop ya asigna
`personas.ver` a Decanato en `PermisosPersonasIdentity` y después elimina
`usuarios.ver` en `RetirarPermisosUsuariosDocentesIdentity`. El Asistente usa
`personas.ver`; no se recrean permisos retirados ni se vuelven a conceder
asignaciones que un administrador haya revocado.

Una base con los IDs anteriores de esta rama queda incompatible deliberadamente.
La recuperación queda a cargo del operador mediante el reset manual de `pr-40`,
separado del deploy ordinario, con las imágenes corregidas disponibles y conservando backups según
el [procedimiento de operación](../docs/operations/migrations-persistence.md).
No se modifica el historial de staging ni de producción, ni el manifiesto
protegido. Los changes archivados conservan los IDs originales como evidencia.

- Cada historial aplica recursos de sus schemas; Identity incluye `audit`.
  `identity.asistente_alcanza_a`
  vive en `database/designaciones/` porque su cuerpo `LANGUAGE sql` referencia
  `designaciones.designaciones` y se parsea al crearse: la cadena de Identity
  corre antes de que ese schema exista.
- `portal.perfiles.persona_id` sigue siendo una referencia lógica: la FK que
  agregaba el asistente no se integró. El lector de catálogo del asistente
  declara esa referencia aparte para el prompt.
- `database/asistente/*.sql` no tiene historial EF: lo aplica `MigradorAsistente`,
  que converge y registra una huella de sus scripts y roles (nombre y OID) en
  `asistente.version_aplicada`. Por eso esos archivos figuran en
  `clasificados` del manifiesto y el contexto `asistente` informa una única
  versión disponible a `--estado-migraciones`/`--script-migraciones`.
