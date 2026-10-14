# Identity: integridad y lecturas selectivas post-auditoría

Alcance: sección 4 y soporte Identity de 1.3 del change
`corregir-integridad-y-rendimiento-post-auditoria`. Fecha: 2026-10-13.
No acredita despliegue ni limpieza de datos de ambientes compartidos.

## Invariantes y errores

- Alta/edición e ingreso consultan `lower(btrim(upn))`. Se conserva el incremental
  `20261013155142_UnicidadUpnNormalizadoIdentity` preexistente, sin cambiar sus bytes.
  El UNIQUE alcanza cuentas inactivas; un duplicado legado aborta el upgrade sin
  fusionar usuario, persona, roles, OID ni TID. `23505` del índice se traduce a
  `identity-upn-conflict`.
- `ProtegerAmbitoRolesIdentity`: cambiar scope con asignaciones vivas se rechaza en
  servicio y SQL. La edición toma `FOR UPDATE` de la fila canónica; una asignación
  lee el rol bajo `FOR SHARE`, dentro de su transacción. El trigger cubre también
  SQL directo. `roles_scope_asignaciones_vivas` y `user_roles_scope_coherente` son
  violaciones `23514`; el servicio ofrece `identity-role-scope-conflict`.
- Revocar una asignación legada incoherente puede cambiar solamente `deleted_at`;
  no puede cambiar usuario, rol ni contexto en ese mismo update. Un regrant se
  valida contra el scope actual. Las revocadas conservan sus atributos históricos.
- Sistema conserva metadatos inmutables, trigger publicado y superficie de permisos.
  Docente requiere materia+carrera; Jefe/personalizado de materia, materia sin
  carrera; personalizado de carrera, carrera sin materia; global, ninguna.
- `UnicidadNombreRolActivoIdentity`: UNIQUE parcial
  `lower(btrim(name)) WHERE is_active`, con lock y preflight. Conserva código,
  permisos e historial; desactivados no reservan el nombre. Sólo `23505` del índice
  de nombre se traduce a `identity-role-name-conflict` (409); no se reinterpretan
  errores de otros constraints como duplicado de nombre.

Son migraciones SQL+wrapper generadas por el scaffolder, no DDL EF paralelo.
Los índices funcionales pertenecen al SQL autorizado; no se inventan propiedades
EF para representar expresiones. Las tablas continúan excluidas de generación EF.

## Contratos de lectura para consumidores

`IConsultasIdentity` agrega:

```csharp
Task<Usuario?> ObtenerUsuarioAsync(Guid usuarioId, CancellationToken ct);
Task<IReadOnlyList<Usuario>> ObtenerUsuariosAsync(IReadOnlyCollection<Guid> usuarioIds, CancellationToken ct);
Task<IReadOnlyList<Persona>> ObtenerPersonasAsync(IReadOnlyCollection<Guid> personaIds, CancellationToken ct);
Task<IReadOnlyList<Usuario>> BuscarCandidatosAsync(FiltroCandidatosIdentity filtro, CancellationToken ct);
```

Cuenta/lote incluyen Persona en la misma consulta y conservan inactivos para
historia. No autorizan nuevas operaciones sobre identidades inactivas. Las
consultas son `AsNoTracking`; Shared no lee schemas de negocio ni Storage.

`FiltroCandidatosIdentity` contiene `Busqueda`, `Limite` (1–50),
`CodigosRolesAdmitidos`, `CodigosRolesExcluidos` y `UsuarioIdsIncluidos`.
La búsqueda exige todas las palabras, ignora case/tildes y busca display name,
nombre, apellido, UPN, legajo y documento. SQL usa `strpos` literal, no comodines.
La función SQL de normalización utiliza la extensión PostgreSQL `unaccent` en
`public`, instalada por un incremental. El upgrade debe ejecutarse con permisos
para esa extensión; ausencia/permisos insuficientes abortan, no sustituyen el
filtrado por un directorio materializado.

Actividad, rol vigente de sistema, palabras y filtro de jerarquía preceden al
límite y la materialización. El consumidor establece su jerarquía: un rol admitido
es suficiente, pero cualquiera de los excluidos elimina el candidato (incluido
un usuario de múltiples roles cuyo principal es superior). `null` en admitidos
permite cualquier rol de sistema; vacío ninguno. `UsuarioIdsIncluidos` permite
excepciones explícitas del consumidor (actor/responsable de proyecto), sin eludir
actividad ni rol de sistema vigente. Orden estable por display name e ID.

Portal resuelve el actor autenticado con `ObtenerUsuarioAsync`; Designaciones consulta personas/actores por lote. Tareas limita la resolución a IDs visibles y usa `BuscarCandidatosAsync` para aplicar rol/jerarquía antes del límite de 50. Estas adopciones están implementadas y verificadas en las suites/evidencias de cada consumidor; no recurren al directorio global como fallback.

## Guard canónico dentro de la transacción del consumidor (1.3)

```csharp
await ProteccionDestinosIdentity.ValidarAsync(
    db.Database.GetDbConnection(),
    db.Database.CurrentTransaction!.GetDbTransaction(),
    usuarioIdsNuevos, personaIdsNuevos, ct);
// Persistir referencia en la misma transacción, luego commit del consumidor.
```

Firma pública:

```csharp
Task ProteccionDestinosIdentity.ValidarAsync(
    DbConnection conexion, DbTransaction transaccion,
    IReadOnlyCollection<Guid> usuarioIds, IReadOnlyCollection<Guid> personaIds,
    CancellationToken ct);
```

El helper exige conexión abierta y transacción de esa conexión. No abre otra,
no realiza writes Identity ni commit. Invoca
`identity.validar_destinos_logicos(uuid[], uuid[])`: personas y luego usuarios,
IDs deduplicados/ordenados, `FOR SHARE` canónico. Exige personas existentes y
usuarios existentes/activos. Espera updates de actividad o mantenimiento, también
los no clave; no alcanza con `FOR KEY SHARE`.

Si una desactivación ganó, la validación reevalúa el destino luego de esperar y
falla con `identity-destination-conflict` (409). Si ganó la referencia, la
administración espera hasta commit/rollback y puede desactivar después: la
referencia queda histórica, no huérfana. Validar sólo referencias nuevas/cambiadas;
las conservadas e históricas inactivas siguen siendo legibles. Los guards de roles
y autoridad del caso de uso siguen a cargo del consumidor, dentro de su operación.

### Mantenimiento físico

Los controllers de usuarios/docentes ofrecen alta/edición/activar/desactivar,
no un DELETE físico ordinario. No se inventó un endpoint. Triggers fail-closed
impiden DELETE de `identity.users` y `identity.personas`, incluso sin referencias
conocidas. No hay flag/GUC runtime de bypass ni FK interschema generalizada.
Desactivación es la operación ordinaria y conserva IDs/referencias.

Mantenimiento extraordinario exige decisión explícita, escritores detenidos,
backup recuperable, diagnóstico y consultas locales de referencias de todos los
consumidores, antes de una intervención aprobada. No se ofrece borrado físico
automático ni saneamiento/merge. Un owner/DBA puede deliberadamente omitir el
protocolo/desactivar triggers o truncar: no se promete aislamiento contra eso.
El diagnóstico del parent complementa esta protección sin escribir datos.

## Formulario de roles

Nombre de creación/edición compara trim/case, manteniendo tildes significativas
como el índice SQL (la normalización de búsqueda es distinta). Se mantiene código
estable y campos de sistema de sólo lectura. Un 409 de nombre/scope/concurrencia
aparece dentro del modal; conserva campos y explica el siguiente paso. Guardar se
deshabilita durante la solicitud, el modal se cierra sólo con éxito y no hay retry
ciego de una versión obsoleta.

## Reproducciones y verificación

Tests persistentes en `backend/tests/ArsDocendi.IntegrationTests/Identity/`:

- `IntegridadIdentityTests`: RED de ingreso/UPN exterior, GREEN; upgrade UPN limpio
  y duplicado, huella exacta de usuarios/personas/roles/eventos y SQL duplicado
  inactivo. Se fija explícitamente el destino incremental UPN para no mezclar
  cambios aditivos posteriores de forma JSON de auditoría con esta reproducción.
- `IntegridadRolesTests`: servicio/SQL scope vivo, revocadas, legado revocable,
  grant-vs-scope con dos sesiones en ambos órdenes y renombres de dos filas
  concurrentes con traducción 409 específica. Barreras observan
  `pg_blocking_pids`, no dependen sólo de sleeps.
- `ConsultasSelectivasIdentityTests` y `CapturaConsultasIdentity`: palabras, case,
  tildes, jerarquía/múltiples roles/excepción por ID antes del límite, crecimiento
  ajeno 70→770, lote histórico inactivo y conteo de comandos. Una consulta por
  búsqueda, una por lote de usuarios y una por lote de personas; sin tracking.
- `DestinosLogicosIdentityTests`: dos sesiones validación-vs-desactivación en ambos
  órdenes, mantenimiento de persona, rollback de referencia consumidora, destino
  inexistente/inactivo y DELETE físico protegido.
- `UpgradeRolesIdentityTests`: upgrade limpio conserva roles/permisos/eventos y
  duplicados legados abortan sin fusión ni stamp de migración.
- Tests vigentes de ingreso, roles y membresías se conservan. El test SQL de scope
  ahora exige `23514` y constraint específico en lugar de `P0001` genérico.

Reproducción integrada (PostgreSQL desechable, sin bases compartidas):

```sh
dotnet build backend/tests/ArsDocendi.IntegrationTests -c Release -m:1 --no-restore
dotnet vstest backend/tests/ArsDocendi.IntegrationTests/bin/Release/net10.0/ArsDocendi.IntegrationTests.dll \
  --TestAdapterPath:backend/tests/ArsDocendi.IntegrationTests/bin/Release/net10.0 \
  --TestCaseFilter:FullyQualifiedName~ArsDocendi.IntegrationTests.Identity \
  --logger:'trx;LogFileName=identity.trx'
pnpm --filter frontend test:run src/features/roles --maxWorkers=1 --testTimeout=30000
pnpm --filter frontend exec eslint src/features/roles
node --test scripts/tests/migrations.test.mjs
node scripts/migrations/validate.mjs --base-ref f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b
dotnet run --project backend/src/ArsDocendi.Host --no-build -c Release -- --validar-recursos
```

Logs/TRX RED y GREEN propios: `.artifacts/identity/` (evidencia local, no dependencia
obligatoria del test). Durante edición simultánea, un harness aislado en
`.artifacts/identity-tests/` permitió ejecutar las mismas migraciones reales Shared
sin compilar módulos incompletos. No reemplaza la regresión integrada final.
Tooling migraciones: 38/38; protección Git verdadera contra el SHA indicado;
recursos compilados compatibles/verificados; frontend roles: 9/9 y ESLint sin
errores. Suite integrada Identity: 75/75, sin skipped, en
`.artifacts/identity/identity-regresiones-green.trx`; test adicional de upgrade de
nombres: 2/2 en `.artifacts/identity/identity-upgrade-nombres-green.trx` (harness
Identity aislado, mismas migraciones embebidas). No se declara un total de 77 de
una única ejecución integrada. `dotnet format whitespace --verify-no-changes`
sobre los archivos C# propios no reportó cambios. Durante la edición paralela, el build frontend tuvo fallos de versiones en Tareas (histórico, no atribuible a Identity). La verificación consolidada del 14-oct-2026 terminó con 403/403 pruebas, ESLint y build completos exit 0; ver la matriz final del change. El parent consolida los totales finales y las adopciones consumidoras.

### Cotejo focal actual de la consulta selectiva

- RED causal: se suprimió temporalmente el predicado de búsqueda SQL; la regresión devolvió un candidato ajeno antes del límite y falló la aserción por ID. Se restauró exactamente el filtro. TRX: `.artifacts/apply-post-auditoria/consultas-selectivas-identity-red/consultas-selectivas-identity-red.trx`.
- GREEN tras restauración: build Release de IntegrationTests sin warnings/errores y `ConsultasSelectivasIdentityTests` **1/1 PASS**, `.artifacts/apply-post-auditoria/consultas-selectivas-identity-green/consultas-selectivas-identity-green.trx`. En esta corrida el test verifica palabras/case/tildes y jerarquía antes de `LIMIT`, crecimiento de candidatos ajenos, cuenta inactiva visible para historia, lote de persona/usuarios y conteo SQL acotado.

### Cotejo de contratos, consumidores y roles (2026-10-14)

- `IConsultasIdentity` está adoptado por Portal para resolver el actor por cuenta, por Designaciones para personas/actores por lote y por Tareas para IDs visibles y candidatos filtrados antes de 50; el diff y las suites específicas de consumidores cubren esas rutas.
- Se añadió `IdentityPersistenciaTests.Rol_materia_personalizado_se_asigna_sin_carrera`: inserción SQL real de un rol custom `materia` y membresía con materia, `carrera_id IS NULL`; **1/1 PASS**, `.artifacts/apply-post-auditoria/rol-materia-sin-carrera/rol-materia-sin-carrera.trx`. Así se prueba la regla vigente y no el escenario retirado que exigía carrera a todo rol de materia.
- Cotejo de roles de sistema: `AdministracionRolesTests.Edicion_persiste_y_protege_el_ambito_de_roles_de_sistema` conserva rechazo; `Renombrar_rol_custom_conserva_codigo_y_rechaza_nombre_duplicado` sólo permite renombre custom. No hay prueba que autorice renombrar metadatos de sistema.
- Suite Identity integrada después del test nuevo: **79/79 PASS**, cero fallos/omisiones, `.artifacts/apply-post-auditoria/identity-suite-4-5/identity-post-auditoria-4-5.trx`. Frontend roles: **9/9 PASS**; ESLint y `dotnet format whitespace --verify-no-changes` focal terminaron exit 0. Build Release: 0 errores y 2 advertencias xUnit2031 preexistentes en `Portal/ConcurrenciaSeccionesTests.cs` (líneas 38 y 64), ajenas a Identity.
- `erroresRol.ts` traduce 409 de nombre/ámbito; `IndexPage` pasa el mensaje a los modales y `roles.test.tsx` verifica que el formulario conserva campos al mostrar el conflicto. El delta vigente protege metadatos del sistema y permite rol custom de materia con materia sin carrera.
