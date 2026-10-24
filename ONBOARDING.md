# Onboarding

Cómo empezar a trabajar en Ars Docendi y con qué flujo. El setup (requisitos, `./scripts/setup.sh`, servidores de desarrollo) está en [README.md](README.md); volvé acá cuando lo tengas corriendo.

## 1. Qué leer (en orden)

1. **[AGENTS.md](AGENTS.md)**: contexto e invariantes comunes a cualquier herramienta. **Es la única lectura obligatoria.**
2. **[CONTRIBUTING.md](CONTRIBUTING.md)**: ramas, pre-commit, commits y review.
3. **[docs/architecture/](docs/architecture/)**: stack, anatomía de módulos y grafo de dependencias.
4. **[docs/quality/golden-principles.md](docs/quality/golden-principles.md)**: los anti-patterns que marcan `/pr-review` y `/evaluate`.
5. **[openspec/specs/](openspec/specs/)**: el comportamiento vigente de la capability que vayas a tocar.

## 2. Reglas fáciles de romper sin querer

Todas están en [AGENTS.md](AGENTS.md#reglas-de-implementación). Las que más se rompen:

- **Cross-module sólo vía Contracts** (#1): desde `Modules.Designaciones` se importa `Modules.Portal.Contracts`, nunca `Modules.Portal`.
- **Feature nueva ⇒ change OpenSpec listo antes del código** (#5; es un hard gate en `/add-feature`). Un bug se corrige con su test de regresión.
- **API, schema o dependencias ⇒ docs en el mismo PR** (#6).
- **Regla institucional ⇒ `BR-<modulo>-NNN`** con fuente y test (#8).
- **Migraciones**: nunca editar SQL publicado ni usar `dotnet ef migrations add`; ver [database/README.md](database/README.md).

## 3. Skills

En Claude Code viven en `.claude/skills/<nombre>/SKILL.md` y se invocan con `/<nombre>`. Para saber cuál usar en cada situación, consultá la tabla de [docs/workflows/README.md](docs/workflows/README.md). Con dos alcanza para empezar: `/opsx:propose` para algo nuevo y `/fix-bug` para algo roto.

`dotnet-modules-guide` y `react-features-guide` se inyectan solas al editar `backend/src/` y `frontend/src/`. Las skills no trabajan solas: vos revisás cada paso y commiteás.

## 4. Flujos comunes

**Nueva feature**

```
1. git checkout develop && git pull && git checkout -b feature/<kebab>
2. /opsx:propose <descripción>   → openspec/changes/<id>/ (proposal + design + specs + tasks)
3. [el equipo aprueba el change]
4. /add-feature <id>             → gates del proyecto + /opsx:apply + PR
5. /pr-review <PR>               → opcional, antes del review humano
6. [merge] → /opsx:archive <id>  → archiva el change y mergea las delta specs
```

**Bug**: `/fix-bug <descripción>` (red-green-refactor). Si el fix toca Contracts o cruza módulos, la skill lo escala a `/opsx:propose`.

**Cambio en un módulo existente**: `/modify-module <X>` analiza el impacto sobre Contracts. Si es breaking, lo escala a `/opsx:propose`.

**Periódico**: `/architecture-drift-check` compara los docs de arquitectura con el código.

## Cuando algo se rompe

| Problema                          | Acción                                                                                                                                                                                                                                                  |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Pre-commit aborta el commit       | Leer el error, arreglar formato o lint, `git add` y re-commit. No usar `--no-verify`                                                                                                                                                                    |
| `pnpm install` falla              | Verificar las versiones de Node y pnpm declaradas en `package.json` (`engines`, `packageManager`)                                                                                                                                                       |
| `dotnet restore` falla            | Verificar el SDK fijado en `global.json`; `dotnet nuget locals all --clear`                                                                                                                                                                             |
| Postgres no levanta               | Ver el error con `docker compose logs postgres`, reiniciar con `docker compose down` y volver a correr `./scripts/setup.sh`. `docker compose down -v` **borra los datos locales**: usalo sólo como reinicio explícito, tras respaldarlos si hacen falta |
| Tests fallan en CI, no localmente | Revisar `.github/workflows/ci.yml` y reproducir sus versiones                                                                                                                                                                                           |
| CI rojo en tu PR                  | `/ci-fix <PR>`                                                                                                                                                                                                                                          |
| Olvidaste archivar post-merge     | `/opsx:archive <id>`                                                                                                                                                                                                                                    |
| Una skill `/<nombre>` no aparece  | Confirmar que existe `.claude/skills/<nombre>/SKILL.md` con `name:` en el frontmatter y reiniciar la herramienta                                                                                                                                        |

Si sigue sin estar claro, preguntale al equipo. Si la confusión se repite, falta documentación: agregala en el PR donde la viviste.
