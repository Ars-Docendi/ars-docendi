# Guía del repositorio

Esta es la fuente de instrucciones para cualquier asistente o herramienta de desarrollo. Los adaptadores específicos deben enlazar este archivo, no copiarlo.

## Contexto

Ars Docendi es un sistema institucional de la UNLaM: backend ASP.NET Core 10, frontend React 19/Vite 8 y PostgreSQL 18. El monorepo contiene cuatro contextos: Designaciones, Aulas, Portal y Tareas.

Leé antes de cambiar:

- [README.md](README.md) para setup y comandos.
- [docs/architecture/](docs/architecture/) para estructura, dependencias, API y datos.
- [openspec/specs/](openspec/specs/) para comportamiento vigente.
- [docs/business-rules/](docs/business-rules/) para reglas institucionales y sus tests.
- Antes de modificar SQL, schema, migraciones o sus snapshots: [database/README.md](database/README.md) para autoría y protección histórica, y [docs/operations/migrations-persistence.md](docs/operations/migrations-persistence.md) para deployment, seed y recuperación.

## Reglas de implementación

1. `Modules.X` no referencia implementaciones `Modules.Y`; la comunicación pública cruza proyectos `Modules.Y.Contracts`.
2. El grafo de dependencias debe permanecer acíclico.
3. Cada módulo expone `GET /api/<modulo>/ping` con `{ module, status: "ok" }`.
4. `ArsDocendi.Shared` se limita a utilidades transversales e infraestructura `identity`/`audit`. Los módulos leen identidad mediante `IConsultasIdentity`; sólo administración la escribe.
5. Una feature nueva requiere un change OpenSpec listo antes del código. Un bug confirmado se corrige con el test de regresión mínimo.
6. Cambios de API, schema o dependencias actualizan la documentación correspondiente en el mismo diff.
7. No crear UI ficticia, botones muertos, abstracciones especulativas ni dependencias innecesarias.
8. Las reglas normativas usan `BR-<modulo>-NNN`, fuente y mapping a tests.
9. Los cambios de UX actualizan su design spec cuando existe.
10. Identificadores, comentarios y documentación propios se escriben en español; los símbolos de frameworks conservan sus nombres.

## Flujo de trabajo

- Preservá cambios existentes que no pertenezcan a la tarea.
- Buscá usos y llamadores antes de modificar una API compartida.
- Preferí código concreto y funciones estándar. Una interfaz interna necesita más de una implementación o una frontera real.
- Las autorizaciones y reglas de negocio se validan en backend aunque el frontend las anticipe.
- No expongas PII, credenciales ni datos de ámbitos ajenos en errores o logs.
- Usá migraciones/SQL versionado para datos y constraints; no edites una base manualmente como solución.
- El entorno de desarrollo local se siembra, la primera vez (base vacía), con `infra/scripts/seed-data/sintetico.sql` (fixtures) y `infra/scripts/seed-data/sga.sql` (carreras, materias y docentes reales del SGA) juntos — ver [README.md](README.md#3-migraciones-y-seed). No se reaplica sobre una base ya sembrada (pisaría ediciones hechas desde la app); resetear al dataset original es una decisión explícita de quien desarrolla. `sga.sql` tiene PII real y por eso **nunca** corre fuera de dev local; staging/PR usan `infra/scripts/seed.sh`, que solo acepta datasets sin datos reales y se ejecuta durante la inicialización. Producción nunca recibe fixtures.

## Migraciones y cambios de base de datos

- El mecanismo es **EF Core + SQL versionado embebido**, no sincronización automática del schema. Editar un SQL histórico no hace que vuelva a ejecutarse.
- **Nunca editar, eliminar ni ampliar SQL o wrappers de una migración ya publicada o aplicada en un ambiente compartido.** Para cambiar tablas, constraints, índices, funciones o datos versionados, crear una nueva migración incremental. No regenerar el baseline ni modificar el manifiesto para ocultar cambios históricos.
- Generar SQL y wrapper juntos desde la raíz con `node scripts/migrations/scaffold.mjs <Contexto> <NombrePascalCase>`; usar `--dry-run` para revisar antes de escribir. Completar el SQL y conservar la asociación y el orden en `[RecursosMigracionSql(...)]`. No crear un SQL aislado esperando que el runner lo descubra automáticamente.
- Revisar dependencias entre schemas y mantener coherentes entidades, mapeos y snapshot EF cuando corresponda. Los snapshots pueden evolucionar; no son operaciones históricas inmutables. No usar `dotnet ef migrations add` para generar un DDL paralelo al SQL autorizado.
- Verificar con `node --test scripts/tests/migrations.test.mjs`, `node scripts/migrations/validate.mjs`, build del backend y `dotnet run --project backend/src/ArsDocendi.Host --no-build -- --validar-recursos`. Para protección contra Git confiable, seguir el procedimiento de `database/README.md`; la validación sin `--base-ref` no sustituye el control de CI. Ejecutar también las pruebas de integración pertinentes en bases desechables.
- Staging/PR conservan datos y adjuntos entre despliegues. No introducir drop, purge, reseed ni restores automáticos como solución a una migración fallida. El seed es de inicialización; los resets y la recuperación son operaciones explícitas separadas. Nunca ejecutar fixtures en prod ni copiar SGA/PII real a staging/PR.
- El baseline consolidado requiere instancias nuevas. Ante historial alpha, IDs desconocidos/discontinuos o schema gestionado sin historial reconocido, abortar sin stamp ni conversión/reset automático. No aplicar migraciones a bases compartidas para probar un cambio.

## Verificación

```bash
dotnet test backend/ArsDocendi.slnx
pnpm --filter frontend test:run
pnpm --filter frontend lint
pnpm --filter frontend build
pnpm format:check
pnpm exec openspec validate --all --strict
```

Ejecutá el subconjunto proporcional al cambio y dejá constancia de cualquier check que el entorno no permita completar.
