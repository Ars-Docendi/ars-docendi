# Contribuir a Ars Docendi

## Ramas

### Permanentes

- **`main`** — Producción. Solo recibe merges desde `develop` (releases) o `hotfix/*` (parches urgentes).
- **`develop`** — Integración. Default branch del repo: todos los PRs de features apuntan acá.

### Temporales

- **`feature/<descripcion-corta>`** — Trabajo nuevo. Sale de `develop`, mergea a `develop` vía PR.
- **`hotfix/<descripcion-corta>`** — Parche urgente sobre producción. Sale de `main`, mergea a `main` **y** a `develop` (dos PRs) para que el fix no se pierda en el próximo release.

### Naming

- Prefijo obligatorio: `feature/` o `hotfix/`.
- Nombre en kebab-case, descripción corta de qué hace la branch.
- Ejemplos: `feature/login-azure-ad`, `feature/reserva-aulas-listado`, `hotfix/swagger-403-en-prod`.

## Flujo de trabajo

### Feature nueva

```pwsh
git checkout develop
git pull
git checkout -b feature/mi-feature
# ... trabajar, commitear ...
git push -u origin feature/mi-feature
```

Después, abrir PR en GitHub con base `develop`.

### Hotfix sobre producción

```pwsh
git checkout main
git pull
git checkout -b hotfix/descripcion
# ... fix, commitear ...
git push -u origin hotfix/descripcion
```

Abrir **dos** PRs: uno a `main`, otro a `develop`. Ambos deben mergearse.

### Release (promoción a producción)

PR de `develop` → `main` cuando se decide cortar release. Sin release branch intermedia.

## Requisitos para mergear un PR

1. CI verde (backend + frontend).
2. 1 aprobación de otro miembro del equipo.
3. Branch al día respecto de la base (puede requerir rebase / merge previo).
4. Conversaciones de review resueltas.

## Commits

Convención libre por ahora. Sugerencia: mensajes claros en imperativo, idealmente en inglés o español de manera consistente dentro del mismo commit.
