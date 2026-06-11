# Workflow: Fix bug (red-green)

Algo está roto, mal, o regresado — NO una nueva capability. Para nuevas capabilities usar [`/add-feature`](./add-feature.md).

## Filosofía: red-green-refactor

Todo bug fix sigue ciclo estricto:

1. **Red** — escribir test que reproduce el bug y falla contra el código actual.
2. **Green** — cambio MÁS chico que vuelve el test verde.
3. **Refactor** — limpieza si es necesario, manteniendo tests verdes.

Garantiza que el bug está verificablemente fixeado y no puede regresar silenciosamente.

## Pre-requisitos

- Entender el síntoma (mensaje de error, reporte del usuario, comportamiento fallido).
- Leer `docs/architecture/dependency-graph.md` para ubicar el módulo probable.

## Steps

### 1. Localizar

- Identificar el módulo afectado usando el path de error, ruta, o componente UI.
- Leer el `Modules.<X>.Contracts/` del módulo y `docs/architecture/domains/<dominio>.md` si existe.

### 2. Check de escalación

Antes de escribir código, determinar el scope:

| Scope                                            | Acción                                                                |
| ------------------------------------------------ | --------------------------------------------------------------------- |
| Un solo módulo, sin cambio de Contracts          | Continuar este workflow — sin spec ni plan                            |
| Toca `Modules.<X>.Contracts` o múltiples módulos | Crear un change con `/opsx:propose` + nota en `docs/plans/backlog.md` |
| Revela brecha arquitectural                      | Escalar a `/add-feature` completo                                     |

### 3. Red — escribir el test que falla

- Agregar test en el directorio de tests del módulo afectado que captura el bug exacto.
- El test DEBE FALLAR contra el código actual. Si no se puede escribir test fallable (e.g. bug puramente visual), documentar reproducción manual en el proposal del change.
- Stagear el test rojo para que la falla quede registrada.

### 4. Green — fix mínimo

- Hacer el cambio **más chico** que vuelve el test rojo verde.
- NO refactorizar, NO agregar features, NO "mejorar" código vecino en este step.
- Correr toda la suite para confirmar sin regresiones.

### 5. Refactor (opcional)

- Si el fix introdujo duplicación o dejó código sucio, limpiar ahora.
- Mantener todos los tests verdes.

### 6. Verificar

1. **Backend**: `dotnet test ArsDocendi.slnx` — todos verdes, incluido el test nuevo.
2. **Frontend** (si tocó): `pnpm --filter frontend lint` + `build` + cualquier test runner.
3. El test rojo (step 3) ahora pasa.
4. Tests existentes siguen pasando.
5. Chequear `docs/quality/golden-principles.md` — ¿el fix violó alguna regla de boundary o patrón?

### 7. Documentación (solo si surfaces cambiaron)

- Si el fix cambió un **contract público**: update `domains/<dominio>.md`, `api-contracts.md`, `dependency-graph.md`.
- Si el fix cambió **schema**: update `data-model.md`.
- Si el fix afecta una **BR-\***: anotar la regresión en `docs/business-rules/<modulo>.md`.
- Para fixes internos chicos: sin doc updates.

### 8. Prevenir recurrencia

- Si la clase de bug puede recurrir (validación faltante, asunción mala de boundary, estado no manejado), agregar regla a `docs/quality/golden-principles.md`.
- Si el fix está diferido o parcial, agregar a `docs/quality/tech-debt.md` con severidad.

### 9. Abrir PR

Ver [`open-pr.md`](./open-pr.md).

## Anti-patterns

- "Drive-by refactor" mientras se arregla un bug.
- Skipear el test rojo "porque obvio".
- Cambiar Contracts en un fix sin escalar a spec.
- Marcar bug "fixeado" sin un test que verifique.
