---
status: draft # draft | active | completed
owner: ""
last_updated: YYYY-MM-DD
module: "" # designaciones | aulas | portal | tareas | shared
severity: "" # blocker | high | medium | low
---

# Bug: &lt;short-description&gt;

## Síntoma

Lo que el usuario ve, mensaje de error, o comportamiento inesperado. Incluir rol del usuario si es relevante (un mismo flujo puede comportarse distinto según rol).

## Comportamiento esperado

Lo que debería pasar.

## Reproducción

Pasos, endpoint + payload, o test case que falla.

## Surface afectada

- [ ] Backend — `backend/src/Modules.<modulo>/`
- [ ] Backend Contracts — `backend/src/Modules.<modulo>.Contracts/`
- [ ] Frontend — `frontend/src/features/<modulo>/`
- [ ] Shared — `backend/src/ArsDocendi.Shared/` o `frontend/src/shared/`

## Causa raíz (se completa durante el fix)

Módulo, archivo, línea — qué está mal y por qué.

## Plan red-green

### Red (test que falla primero)

Describir o escribir el test que captura el bug. El test debe **fallar** contra el código actual y **pasar** una vez aplicado el fix.

- Ubicación del test: _(ej. `backend/tests/Modules.Designaciones.Tests/DesignacionesServiceTests.cs`)_
- Aserción: _(qué chequea)_

### Green (fix mínimo)

El cambio más chico que hace pasar el test rojo sin romper los existentes.

### Refactor (si corresponde)

Cleanup posterior al green manteniendo los tests verdes.

## Verificación del fix

- [ ] Test rojo falla contra el código actual
- [ ] Green fix hace pasar el test
- [ ] Suite de tests existente sigue pasando
- [ ] Spot-check manual (si es bug de UI)

## Check de escalación

- [ ] El fix queda dentro de **un módulo** y NO cambia `*.Contracts` → no necesita spec/plan
- [ ] El fix toca **contracts o múltiples módulos** → usar este `_bug-template.md` como spec + nota en `docs/plans/backlog.md`
- [ ] El fix revela una **brecha arquitectural** → escalar a `/add-feature` completo

## Post-fix

- [ ] Si la clase de bug es prevenible, agregar regla a `docs/quality/golden-principles.md`
- [ ] Si el fix se difiere, agregar a `docs/quality/tech-debt.md`
- [ ] Si el bug viola una BR-\*, anotar regresión en `docs/business-rules/<modulo>.md`
