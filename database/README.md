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

Aulas sigue integrado sin tablas ni baseline artificial. Tareas incorpora sus
migraciones SQL en `tareas.__EFMigrationsHistory` y se ejecuta después de Identity,
Storage, Designaciones, Aulas y Portal; sus timestamps se ordenan dentro de su
propio contexto, no globalmente. Antes del corte,
Designaciones/Portal/Aulas/Tareas usaban el historial compartido de `public`;
el cambio de ubicación es deliberado y **no convierte bases anteriores**.
El preflight debe rechazar historia alpha, IDs desconocidos/discontinuos y schemas
gestionados sin historial reconocido antes de cualquier escritura. No hacer
`stamp`, `DROP` ni resets automáticos sobre esos destinos.

## Integración de incrementales Identity y Tareas

Las dos migraciones Identity incorporadas desde la rama anterior tenían IDs
anteriores al baseline. Se renumeran sus wrappers (incluido el nombre de archivo),
sin tocar los SQL entrantes ni los SQL/wrappers publicados del baseline:

| ID anterior                              | ID integrado                             |
| ---------------------------------------- | ---------------------------------------- |
| `20260924000000_PermisosTareas`          | `20261005000400_PermisosTareas`          |
| `20261003000000_CuentaMicrosoftUsuarios` | `20261005000500_CuentaMicrosoftUsuarios` |

EF aplica primero `20261005000000_BaselineIdentityYAudit` y luego ambos
incrementales. El baseline ya contiene `tareas.ver` y `tareas.gestionar` y sus
asignaciones para Secretaría, Administrativo y sys_admin. El SQL incremental
conserva su `ON CONFLICT`: agrega `proyectos.gestionar` y amplía las asignaciones
sin duplicar permisos. El baseline no contiene `azure_tid`; el incremental de
cuentas agrega esa columna, permite una cuenta aún no vinculada y retira los
`azure_oid` provisionales, tal como declara su SQL original. No adoptar ni
renombrar historial existente de la rama alpha: el preflight sigue rechazándolo.

Los cinco wrappers entrantes declaran `[RecursosMigracionSql(...)]` y consumen
esa metadata mediante `AplicarRecursosSql`, la API existente del repositorio
(no existe `RecursosSql.LeerDeMigracion`). Tareas conserva sus tres IDs y su
orden propio: schema, catálogos, IDs de catálogos.

`MigracionesRuntimeTests` verifica instalación nueva, preview sin escrituras,
upgrade desde el baseline con datos, inventario de permisos sin duplicados y
reejecución sin nuevas filas de auditoría. `IngresoMicrosoftTests` usa materias
canónicas con códigos numéricos de cinco dígitos y carrera en el ámbito del rol,
no en la entidad Materia. La validación Git contra el SHA completo de HEAD,
el build Release y `--validar-recursos` complementan las pruebas reales con
PostgreSQL desechable. Si `dotnet test` descubre cero tests, ejecutar el assembly
Release con `dotnet vstest` y verificar los totales del TRX.

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
Los `*ModelSnapshot.cs` representan el estado EF actual y pueden evolucionar;
no son operaciones históricas inmutables. Los `.Designer.cs` históricos sí se
protegen. Editar/eliminar un archivo protegido, retirar su entrada o cambiar su
hash/clasificación histórica falla con exit 1. Agregar una migración válida se
permite sin una nueva versión global.

### Congelar el corte revisado (una única vez)

Sólo después de terminar y revisar **todo** el baseline consolidado:

```bash
node scripts/migrations/manifest.mjs --dry-run
node scripts/migrations/manifest.mjs
node scripts/migrations/validate.mjs
```

El generador escribe `database/migraciones-protegidas.json` con versión de
formato 1, hashes SHA-256 de SQL/wrappers y `clasificados`. Nunca debe capturar
archivos intermedios ni volver a incluir las migraciones alpha eliminadas.
Si ya existe un manifiesto, rechaza archivos alterados/eliminados antes de
regenerar; no hay `--force` ni flag para saltar protección.

Las excepciones SQL requieren una ruta **exacta** y motivo, no globs. Si existe
un recurso de soporte sin consumidor EF, proporcionar un JSON de clasificaciones:

```json
{
  "identity/soporte_bootstrap.sql": "Soporte administrativo de bootstrap; no se aplica desde migraciones EF."
}
```

```bash
node scripts/migrations/manifest.mjs --clasificaciones inventario-soporte.json
```

El ejemplo no autoriza crear ese recurso: su existencia y la clasificación se
revisan juntas. Se rechazan excepciones vacías, inexistentes, duplicadas con un
consumidor o modificaciones a clasificaciones históricas.

Para el primer PR, cuya base alpha todavía no contiene manifiesto, una persona
revisora debe fijar el **SHA completo del commit que ya contiene el inventario
final aprobado** en la variable de repositorio `CORTE_MIGRACIONES_REVISADO`.
No usar el HEAD mutable del PR, un branch/tag o un SHA calculado en el workflow.
El check requiere que **la base y el corte aprobado sean ancestros del candidato**
y compartan historia Git. Pueden estar en ramas divergentes: la base puede haber
avanzado después de crear el corte, antes de integrarlo. No se exige que el corte
descienda del SHA base actual. Se validan los bytes del corte contra su inventario
y después contra el PR; siguen rechazándose modificaciones o eliminaciones de
los archivos protegidos, incluso si el PR altera sus hashes:

```bash
node scripts/migrations/validate.mjs --base-ref "$BASE_REF" --corte-revisado "$SHA_CORTE_APROBADO"
```

Retirar la variable después de integrar el corte. El workflow sólo la pasa si
el SHA base todavía no contiene `database/migraciones-protegidas.json`; si la
variable permanece configurada, se omite automáticamente cuando la base ya está
protegida y se compara contra ese SHA base, no contra el corte antiguo.
El CLI sigue rechazando `--corte-revisado` explícito cuando la base contiene
manifiesto: no se puede repetir el squash.
Si falta el commit/inventario aprobado o la historia Git, CI falla cerrada. Esto
no migra una instancia alpha ni habilita edición de historia ya aplicada.

```bash
# Aplicación one-shot, sin listener HTTP
dotnet run --project backend/src/ArsDocendi.Host -- --migrate
```

La reejecución sólo es no-op para historia compatible. `Down` del baseline falla
explícitamente: recuperar un backup en un destino aislado y una versión
compatible, no borrar el schema para recuperar una instancia persistente.

## Evidencia del squash

La instalación alpha se ejecutó desde SQL generado por EF del `git archive HEAD`
aislado, antes de eliminar la historia. PostgreSQL 18.4 desechable; bytes SQL y
dumps por stdin/stdout, sin bind mounts. `.artifacts/baseline/inventario.md`
registra dumps, comandos y comparación exacta normalizada, excluyendo únicamente
histories EF y timestamps de instalación de catálogos. No contiene SGA ni datos
personales reales. La comparación de este corte no es una matriz permanente de
upgrades históricos ni un detector de drift de producción.
