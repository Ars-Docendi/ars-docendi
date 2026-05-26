# Workflow: Architecture proposal

Bootstrap único que llena `docs/architecture/*` con info real. Corre **después de `/init-project`** y **antes** de cualquier `/create-module` o `/add-feature`.

## Cuándo usar

- `docs/product/brief.md` ya tiene contenido real (post `/init-project`).
- `docs/architecture/*.md` todavía coinciden con el template scaffold.

## Artefactos producidos

| Archivo                                  | Rol                                             |
| ---------------------------------------- | ----------------------------------------------- |
| `docs/architecture/stack.md`             | Tabla de apps + packages, decisiones, rationale |
| `docs/architecture/module-anatomy.md`    | Layout de módulo .NET, layer rules              |
| `docs/architecture/dependency-graph.md`  | Mermaid DAG + edge registry                     |
| `docs/architecture/api-contracts.md`     | Base URL, auth, error shape, endpoint table     |
| `docs/architecture/data-model.md`        | ORM, entidades, ER, índices, PII/retención      |
| `docs/architecture/infrastructure.md`    | Ambientes, deploy, health, logs, monitoring     |
| `docs/architecture/domains/<dominio>.md` | Uno por cada bounded context                    |

## Flujo

### 1. Leer contexto de producto

Abrir `docs/product/brief.md` + `vision.md` + `design-principles.md`. Si alguno contiene placeholder, parar y pedir `/init-project` primero.

### 2. Aclarar arquitectura

Preguntar sobre:

1. **Topología de runtime** — qué apps corren (backend / frontend / workers / etc) y cómo se comunican.
2. **Stack** — lenguajes, frameworks, ORM, runtime versions.
3. **Dominios (bounded contexts)** — qué módulos backend. Cada uno será `domains/<nombre>.md`.
4. **API pública** — endpoints, webhooks, eventos en el límite del sistema + esquema de auth.
5. **Persistencia** — entidades principales, relaciones, PII, retención.
6. **Infrastructure** — hosting target, CI/CD trigger, observability.

### 3. Redactar los docs

Llenar los 7 archivos + un `domains/<dominio>.md` por bounded context. Reglas:

- Sin placeholder donde haya respuesta del usuario.
- Donde no haya respuesta, marcar `_(needs owner input: <qué>)_`.
- `dependency-graph.md`: regenerar el Mermaid + edge registry desde los dominios propuestos. Cada edge debe corresponder a una interacción mencionada en la descripción.
- `infrastructure.md`: preservar secciones de hardening checklist y backup strategy (son boilerplate útil), reemplazar lo específico.

### 4. Aprobar y commitear

- Mostrar al equipo. Iterar con feedback.
- Crear branch `chore/architecture-proposal`, commit, push.
- Abrir PR a `develop` (ver [open-pr.md](./open-pr.md)).

## Reglas duras

- **Solo** editar `docs/architecture/`. Cualquier otro path está fuera de scope.
- **Sin código**, sin migrations, sin `docs/product/`, `docs/plans/`, `docs/business-rules/`.
- **Una vez por proyecto**. Si `stack.md` ya tiene contenido no-template, confirmar antes de sobreescribir.

## Handoff

Después del merge:

- `/create-module` — para crear el primer módulo .NET propuesto en `dependency-graph.md`.
- `/plan-feature` — para empezar specs y planes.
- `/architecture-drift-check` (recurrente) — cuando el código empiece a aterrizar.
