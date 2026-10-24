# Guía del repositorio

Esta es la fuente de instrucciones para cualquier asistente o herramienta de desarrollo. Los adaptadores específicos deben enlazar este archivo, no copiarlo. Única excepción: el glue generado de OpenSpec (`.claude/skills/openspec-*`, `.claude/commands/opsx/`, `.agents/skills/openspec-*`) se versiona tal como lo emite `openspec update` y no se edita a mano ([CONTRIBUTING → Glue OpenSpec](CONTRIBUTING.md#glue-openspec-política-d7)).

## Contexto

Ars Docendi es un sistema institucional de la UNLaM: backend ASP.NET Core 10, frontend React 19/Vite 8 y PostgreSQL 18. El monorepo contiene cuatro contextos de dominio (Designaciones, Aulas, Portal y Tareas) más los servicios transversales Identity, Storage e Inferencia (ver [docs/architecture/dependency-graph.md](docs/architecture/dependency-graph.md)).

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
11. Un test nuevo debe proteger una regla de negocio, una garantía de seguridad o de datos, o un flujo de usuario que ningún otro test cubra ya. No se agregan tests que cuentan, verifican forma, prueban el framework, son cosméticos o miden rendimiento. Criterios completos en [docs/quality/golden-principles.md → Tests](docs/quality/golden-principles.md#tests).

## Flujo de trabajo

- Preservá cambios existentes que no pertenezcan a la tarea.
- Buscá usos y llamadores antes de modificar una API compartida.
- Preferí código concreto y funciones estándar. Una interfaz interna necesita más de una implementación o una frontera real.
- Las autorizaciones y reglas de negocio se validan en backend aunque el frontend las anticipe.
- No expongas PII, credenciales ni datos de ámbitos ajenos en errores o logs.
- Usá migraciones/SQL versionado para datos y constraints; no edites una base manualmente como solución.
- Seed: `sga.sql` (no versionado) tiene PII real y **nunca** corre fuera de dev local; no se reaplica sobre una base ya sembrada, y producción nunca recibe fixtures. El procedimiento está en [README.md](README.md#3-migraciones-y-seed).

## Migraciones y cambios de base de datos

- El mecanismo es **EF Core + SQL versionado embebido**, no sincronización automática del schema. Editar un SQL histórico no hace que vuelva a ejecutarse.
- **Nunca editar, eliminar ni ampliar SQL o wrappers de una migración ya publicada o aplicada en un ambiente compartido.** Para cambiar tablas, constraints, índices, funciones o datos versionados, crear una nueva migración incremental. No regenerar el baseline ni modificar el manifiesto para ocultar cambios históricos.
- Generar SQL y wrapper juntos con `node scripts/migrations/scaffold.mjs <Contexto> <NombrePascalCase>`. No crear un SQL aislado esperando que el runner lo descubra automáticamente.
- Revisar dependencias entre schemas y mantener coherentes entidades, mapeos y snapshot EF cuando corresponda. Los snapshots pueden evolucionar; no son operaciones históricas inmutables. No usar `dotnet ef migrations add` para generar un DDL paralelo al SQL autorizado.
- Verificar con los comandos de [database/README.md → Autoría posterior](database/README.md#autoría-posterior) y las pruebas de integración pertinentes en bases desechables.
- Staging/PR conservan datos y adjuntos entre despliegues. No introducir drop, purge, reseed ni restores automáticos como solución a una migración fallida. El seed es de inicialización; los resets y la recuperación son operaciones explícitas separadas. Nunca ejecutar fixtures en prod ni copiar SGA/PII real a staging/PR.
- El baseline consolidado requiere instancias nuevas. Ante historial alpha, IDs desconocidos/discontinuos o schema gestionado sin historial reconocido, abortar sin stamp ni conversión/reset automático. No aplicar migraciones a bases compartidas para probar un cambio.

## Verificación

Esta es la lista única de checks; los skills y playbooks la enlazan. CI (`.github/workflows/ci.yml`) corre los mismos. Ejecutá los que correspondan al cambio:

| Tocaste                       | Comando                                                                                                                                                                                                                   |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Backend                       | `dotnet test backend/ArsDocendi.slnx`                                                                                                                                                                                     |
| SQL, migraciones o manifiesto | `node --test scripts/tests/migrations.test.mjs`, `node scripts/migrations/validate.mjs` y, tras `dotnet build -c Release`, `dotnet run --project backend/src/ArsDocendi.Host --no-build -c Release -- --validar-recursos` |
| Frontend                      | `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build`                                                                                                                          |
| `infra/`                      | `node --test infra/tests/*.test.mjs`                                                                                                                                                                                      |
| Docs, specs o cualquier `.md` | `pnpm format:check`, `node scripts/docs/check-links.mjs`, `pnpm exec openspec validate --all`                                                                                                                             |

Dejá constancia de cualquier check que el entorno no permita completar.

## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
