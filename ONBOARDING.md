# Onboarding y uso del harness

Cómo usar el workflow y las skills del proyecto. **Tres audiencias** — leé la que te corresponde.

| Si vos sos...                                        | Saltá a                                                              |
| ---------------------------------------------------- | -------------------------------------------------------------------- |
| Nuevo en el equipo, recién clonaste el repo          | [§1 Developer nuevo](#1-developer-nuevo)                             |
| El primero en llenar el contenido inicial de `docs/` | [§2 Primer mantenedor de contexto](#2-primer-mantenedor-de-contexto) |
| Ya conocés el sistema y querés refrescar comandos    | [§3 Cheat sheet](#3-cheat-sheet)                                     |

> Este harness es **project-scoped**: todas las skills viven en `.claude/skills/` y se versionan. No requieren framework global. El detalle paso a paso de cada workflow vive en su `SKILL.md` (una sola fuente); en `docs/workflows/` solo queda `open-pr.md` (referencia canónica) + un índice.

---

## 1. Developer nuevo

### 1.1 Pre-requisitos

| Herramienta      | Versión mínima   | Verificar                |
| ---------------- | ---------------- | ------------------------ |
| .NET SDK         | 10.0.x           | `dotnet --version`       |
| Node             | 20.19+           | `node --version`         |
| pnpm             | 9.x (o corepack) | `pnpm --version`         |
| Docker + Compose | Reciente         | `docker compose version` |
| `gh` CLI         | Reciente         | `gh --version`           |

### 1.2 Levantar el proyecto

El setup está en [README.md](README.md) (`./scripts/setup.sh` + arranque de dev servers). Volvé acá cuando lo tengas corriendo.

### 1.3 Qué leer (en orden)

1. **[CLAUDE.md](CLAUDE.md)** — contexto, módulos, roles, **13 invariantes** no negociables, skills. **Única lectura realmente obligatoria.**
2. **[CONTRIBUTING.md](CONTRIBUTING.md)** — gitflow, pre-commit, code review, commits.
3. **[docs/architecture/stack.md](docs/architecture/stack.md)** + **[module-anatomy.md](docs/architecture/module-anatomy.md)** + **[dependency-graph.md](docs/architecture/dependency-graph.md)**.
4. **[docs/quality/golden-principles.md](docs/quality/golden-principles.md)** — anti-patterns; los flagga `/pr-review` y `/evaluate`.
5. **[docs/workflows/README.md](docs/workflows/README.md)** — índice "¿qué skill uso?".

Después, [`docs/architecture/domains/`](docs/architecture/domains/) para cada módulo en profundidad.

### 1.4 Cómo trabajan las skills

Viven en `.claude/skills/<nombre>/SKILL.md`, se invocan con `/<nombre> [args]`. Tres tipos:

- **Interactivas** (las invocás vos): `/opsx:propose`, `/add-feature`, `/fix-bug`, `/pr-review`, etc.
- **Path-scoped** (auto-activan): `dotnet-modules-guide` al editar `backend/src/Modules.*`, `react-features-guide` al editar `frontend/src/`.

No hace falta memorizarlas. Empezás con dos: `/opsx:propose` para algo nuevo, `/fix-bug` para algo roto. El resto se aprende. Los flujos concretos están en [§3.3](#33-flujos-comunes). **No es automático ciego — vos revisás cada paso y commiteás.**

### 1.5 Reglas críticas para no romper nada

Las **13 invariantes** del [CLAUDE.md](CLAUDE.md#invariantes-no-negociables) son no negociables. Las más fáciles de romper sin querer:

- **Cross-module: solo vía Contracts**. Desde `Modules.Designaciones` importás `Modules.Portal.Contracts`, nunca `Modules.Portal` interno ni `Internal/` ajeno.
- **Nueva feature ⇒ change OpenSpec apply-ready ANTES de código** (hard gate en `/add-feature`).
- **Cambios de API/schema/deps ⇒ docs en el MISMO PR** (`api-contracts.md`, `data-model.md`, `dependency-graph.md`, `domains/<x>.md`).
- **Bug fixes ⇒ red-green obligatorio**.
- **Compliance**: regla de normativa institucional ⇒ `BR-<modulo>-NNN` en `docs/business-rules/<modulo>.md` con cita.

---

## 2. Primer mantenedor de contexto

Para vos si te toca **completar el contenido inicial** de `docs/`.

### 2.1 Estado actual de `docs/`

| Carpeta                                                   | Estado                                                                          | Acción                                                                          |
| --------------------------------------------------------- | ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| `product/{brief,vision,design-principles}.md`             | ✅ Completos                                                                    | Revisar/precisar; métricas pueden necesitar baseline real                       |
| `product/designs/`                                        | 📝 Placeholder (herramienta UX TBD)                                             | Completar cuando el equipo decida                                               |
| `architecture/{stack,module-anatomy,dependency-graph}.md` | ✅ Completos                                                                    | `dependency-graph` se actualiza SIEMPRE que cambie un edge cross-module         |
| `architecture/api-contracts.md`                           | ⚠️ Parcial (solo ping endpoints)                                                | Completar a medida que se definan endpoints (mismo PR — invariante #6)          |
| `architecture/data-model.md`                              | ⚠️ Parcial                                                                      | Completar a medida que se definan entidades; marcar PII explícita               |
| `architecture/infrastructure.md`                          | ⚠️ Skeleton (TBD)                                                               | Completar cuando UNLaM provisione VMs                                           |
| `architecture/domains/<modulo>.md`                        | ⚠️ Esqueleto                                                                    | Llenar entidades/API/BR a medida que cada módulo evolucione                     |
| `plans/backlog.md`                                        | 📝 Vacío                                                                        | Ideas pendientes de proponer; features activas en `openspec/changes/`           |
| `quality/{golden-principles,grading-criteria}.md`         | ✅ Completos                                                                    | Sumar reglas cuando aparezcan anti-patterns                                     |
| `quality/{scorecard,tech-debt}.md`                        | 📝 Se llenan con `/evaluate` y al detectar deuda                                | —                                                                               |
| `workflows/`                                              | `open-pr.md` (referencia) + `README.md` (índice); el detalle vive en las skills | Estable                                                                         |
| `business-rules/`                                         | 📝 Solo `_template.md`                                                          | **Crear un .md por módulo con cada BR-\*** — crítico para compliance (ver §2.2) |
| `references/`                                             | 📝 Solo README                                                                  | Agregar `<lib>-llms.txt` cuando una lib se use intensivamente                   |

### 2.2 Business rules — TAREA CRÍTICA INICIAL

La tarea más importante del primer mantenedor: identificar las **reglas reglamentarias** del Departamento de Ingeniería UNLaM y registrarlas en `docs/business-rules/<modulo>.md`.

**Proceso**:

1. Pedir al cliente (Secretaría Académica / Departamento) los documentos normativos: estatuto UNLaM, régimen de docencia, normativas departamentales sobre designaciones/mesas/reservas, disposiciones que afecten el flujo.
2. Por cada módulo, crear `docs/business-rules/<modulo>.md` desde `_template.md`.
3. Por cada regla a implementar: asignar `BR-<modulo>-NNN`; llenar `Statement`, `Rationale`, `Provenance: from_regulation`, `Fuente normativa` (cita exacta), `Ejemplos` (positivo + negativo), `Roles afectados`.
4. **No inventar reglas**. Si no está en la normativa pero parece obvia, marcarla `inferred_from_code` o `confirmed_user` y validarla con el cliente.

```markdown
### BR-designaciones-001 Solo el Coordinador de la carrera puede aprobar designaciones de esa carrera

- **Statement:** El Coordinador de Carrera solo aprueba/rechaza designaciones de docentes de carreras bajo su coordinación.
- **Provenance:** `from_regulation`
- **Fuente normativa:** Régimen de Designaciones Docentes UNLaM, Art. 14
- **Ejemplos:** Positivo: Coord. de Informática aprueba designación de Programación I. Negativo: NO puede aprobar una de Cálculo (otra carrera).
- **Roles afectados:** Coordinador de Carrera, Jefe de Cátedra
```

**Cada BR debe tener un test** cuando la regla se implemente; el mapping va en la sección "Test mapping" del mismo archivo (lo cubre `/add-tests --lane business`).

### 2.3 Resto del contenido (a medida que aparece)

- **api-contracts / data-model / domains**: completar la fila/entidad en el MISMO PR que introduce el endpoint, migration EF Core, o cambio de módulo.
- **infrastructure**: VMs, deploy process, secrets, backup, reverse proxy, hardening, monitoring — cuando UNLaM entregue las VMs. Ahí también crear los runbooks (`deploy.md`, `troubleshooting.md`, `restore-from-backup.md`) y recrear las skills de ops/auditoría retiradas (ver `docs/quality/tech-debt.md` TD-002/TD-003).
- **design specs**: hasta elegir herramienta UX, son `.md` en `docs/product/designs/` siguiendo `_design-spec-template.md`.

### 2.4 Re-bootstrap (rehacer contenido inicial)

`/init-project` reescribe `docs/product/*`; `/architecture-proposal` reescribe `docs/architecture/*`. **Ambas sobreescriben** — corré en branch separada y diffeá contra `develop` antes de mergear.

---

## 3. Cheat sheet

### 3.1 Comandos shell

```bash
./scripts/setup.sh                                  # setup (primera vez / reset)
dotnet run --project backend/src/ArsDocendi.Host    # dev backend
pnpm --filter frontend dev                          # dev frontend
dotnet test backend/ArsDocendi.slnx                 # tests backend
dotnet test --filter "FullyQualifiedName~NombreTest"   # uno solo
dotnet build backend/ArsDocendi.slnx                # build backend
pnpm --filter frontend build|lint                   # build / lint frontend
pnpm format[:check]                                 # formatea / verifica
pnpm generate-indexes                               # índice de business-rules
openspec list | openspec view <id>                  # changes OpenSpec
docker compose up -d | ps | logs -f api | down      # postgres local
```

### 3.2 Skills más usadas

| Skill / Comando  | Cuándo                            | Ejemplo                                        |
| ---------------- | --------------------------------- | ---------------------------------------------- |
| `/opsx:propose`  | Empezar una feature nueva         | `/opsx:propose listado de aulas por fecha`     |
| `/add-feature`   | Implementar change aprobado       | `/add-feature listado-aulas-disponibles`       |
| `/opsx:apply`    | Ejecutar tasks de un change       | `/opsx:apply listado-aulas-disponibles`        |
| `/opsx:archive`  | Post-merge: archivar change       | `/opsx:archive listado-aulas-disponibles`      |
| `/fix-bug`       | Algo roto / regresión             | `/fix-bug fecha DD/MM en API pero MM/DD en UI` |
| `/pr-review`     | Review estructurado de PR         | `/pr-review 42`                                |
| `/ci-fix`        | CI fallido en tu PR               | `/ci-fix 42`                                   |
| `/add-tests`     | Cubrir gaps de tests              | `/add-tests designaciones --lane business`     |
| `/create-module` | Módulo .NET nuevo                 | `/create-module Examenes`                      |
| `/modify-module` | Cambio en módulo existente        | `/modify-module Designaciones`                 |
| `/evaluate`      | Autocrítica de feature completada | `/evaluate exportar-designaciones-excel`       |

### 3.3 Flujos comunes

**Nueva feature**

```
1. git checkout develop && pull; git checkout -b feature/<kebab>
2. /opsx:propose <descripción>   → openspec/changes/<id>/ (proposal+design+specs+tasks)
3. [equipo aprueba]              → change apply-ready
4. /add-feature <id>             → gates del proyecto + ejecución vía /opsx:apply + PR
5. /pr-review <PR>               → opcional
6. [merge] → /opsx:archive <id>  → archiva change + mergea delta specs
```

**Bug fix simple**

```
1. branch feature/fix-<corto>
2. /fix-bug <descripción>        → red-green-refactor
3. [revisar diff: sin drive-by] → PR (la skill suele abrirlo) → merge
```

**Bug que toca Contracts / múltiples módulos**: `/fix-bug` detecta la escalación en su step 2 e indica crear un change con `/opsx:propose`; documentás consumidores afectados en el PR.

**Cambio cross-module sin bug**: `/modify-module <X>` analiza impacto Contracts; si es breaking escala a `/opsx:propose`, si es aditivo implementás y actualizás `dependency-graph.md`.

**Antes de release / periódico**: `/evaluate <feature>` (score + scorecard) y `/architecture-drift-check` (docs vs código). _(Las pasadas de `/security-audit` y `/test-gap-monitor` están diferidas hasta que haya más código — ver tech-debt TD-003.)_

### 3.4 Path-scoped guides (auto-inyectan)

| Path                                                  | Guide                                                   |
| ----------------------------------------------------- | ------------------------------------------------------- |
| `backend/src/Modules.*/`, `ArsDocendi.{Host,Shared}/` | `dotnet-modules-guide` (layers, Contracts, ping)        |
| `frontend/src/`                                       | `react-features-guide` (features aisladas, React Query) |

### 3.5 Convenciones siempre presentes

- **Pre-commit falla → arreglar, NO `--no-verify`**.
- **Change OpenSpec apply-ready ANTES de código** (hard gate en `/add-feature`).
- **Cross-module solo vía Contracts** (#1) · **schema/API → docs mismo PR** (#6) · **red-green en bugs** (#9) · **BR-\* para reglas reglamentarias** (#11).
- **Sin AI attribution en commits**: el proyecto NO usa `Co-Authored-By: Claude` ni similares.

---

## Cuando algo se rompe

| Problema                          | Acción                                                                                              |
| --------------------------------- | --------------------------------------------------------------------------------------------------- |
| Pre-commit aborta el commit       | Leer el error, arreglar formato/lint, `git add` modificados, re-commit                              |
| `pnpm install` falla (peer deps)  | Node ≥ 20.19 y pnpm ≥ 9; si persiste `rm -rf node_modules pnpm-lock.yaml && pnpm install`           |
| `dotnet restore` falla            | `dotnet --version` ≥ 10.0.x; `dotnet nuget locals all --clear`                                      |
| Postgres no levanta               | `docker compose down -v` (borra datos) y reintentar                                                 |
| Tests fallan en CI, no localmente | Env de CI distinto — revisar `.github/workflows/ci.yml` y reproducir versiones                      |
| CI rojo en tu PR                  | `/ci-fix <PR>`                                                                                      |
| Olvidaste archivar post-merge     | `/opsx:archive <id>`                                                                                |
| Skill `/<nombre>` no aparece      | Confirmá `.claude/skills/<nombre>/SKILL.md` con frontmatter `name:` correcto; reiniciar Claude Code |

## Si te perdés

- **Navegación general** + **invariantes**: [CLAUDE.md](CLAUDE.md)
- **¿Qué skill uso?**: [docs/workflows/README.md](docs/workflows/README.md)
- **Calidad / anti-patterns**: [docs/quality/golden-principles.md](docs/quality/golden-principles.md)
- **Abrir un PR canónico**: [docs/workflows/open-pr.md](docs/workflows/open-pr.md)

Si sigue sin estar claro, preguntale al equipo. Si la confusión se repite, falta documentación — agregala en el PR donde la viviste.
