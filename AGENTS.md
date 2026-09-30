# Guía del repositorio

Esta es la fuente de instrucciones para cualquier asistente o herramienta de desarrollo. Los adaptadores específicos deben enlazar este archivo, no copiarlo.

## Contexto

Ars Docendi es un sistema institucional de la UNLaM: backend ASP.NET Core 10, frontend React 19/Vite 8 y PostgreSQL 18. El monorepo contiene cuatro contextos: Designaciones, Aulas, Portal y Tareas.

Leé antes de cambiar:

- [README.md](README.md) para setup y comandos.
- [docs/architecture/](docs/architecture/) para estructura, dependencias, API y datos.
- [openspec/specs/](openspec/specs/) para comportamiento vigente.
- [docs/business-rules/](docs/business-rules/) para reglas institucionales y sus tests.

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

<!-- CODEGRAPH_START -->

## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
