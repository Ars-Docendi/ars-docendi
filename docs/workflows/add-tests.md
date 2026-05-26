# Workflow: Add tests

Agregar tests faltantes a un módulo o feature. Dos lanes según naturaleza del test.

## Lanes

| Lane          | Cuándo                                 | Output                                                                           |
| ------------- | -------------------------------------- | -------------------------------------------------------------------------------- |
| **Business**  | Tests de reglas de negocio (BR-\*)     | Test verifica una BR-\* específica con cita en `docs/business-rules/<modulo>.md` |
| **Technical** | Smoke tests, render tests, integration | Tests sin amarre directo a BR-\*; cubren caminos felices y casos básicos         |

## Pre-requisitos

- Identificar el módulo / feature objetivo.
- Si es lane business: leer `docs/business-rules/<modulo>.md` (crear desde template si no existe).
- Verificar test runner del módulo (backend: `dotnet test`, frontend: TBD — actualmente no hay runner configurado).

## Steps — lane Business (BR-\*)

### 1. Descubrir BRs sin test

Leer `docs/business-rules/<modulo>.md` sección **Test mapping**. Identificar BR-\* sin entry.

### 2. Por cada BR sin test

1. Releer el `Statement`, `Rationale`, `Ejemplos` del BR.
2. Decidir tipo de test: unit (Service), integration (con DB), o e2e (con HTTP).
3. Escribir el test que verifica el `Statement` con al menos un caso positivo y un negativo (de los `Ejemplos`).
4. Correr — si pasa, agregar entry a **Test mapping** del BR-\*:

```markdown
| BR-designaciones-001 | backend/tests/Modules.Designaciones.Tests/DesignacionesServiceTests.cs::QueRechazaSiCoordinadorNoEsDeCarrera | unit | Verifica autorización por rol |
```

### 3. Si una BR no es testeable

- Documentar por qué (e.g. requiere mock complejo de Azure AD).
- Marcar la BR con tag `_test-deferred_` y mover el cumplimiento a verificación manual en el plan correspondiente.
- Agregar a `docs/quality/tech-debt.md` si la deuda es significativa.

## Steps — lane Technical

### 1. Identificar gaps

- Correr `/test-gap-monitor` (ver [test-gap-monitor.md](./test-gap-monitor.md)) o mirar coverage manualmente.
- Priorizar: módulos críticos primero (Designaciones > Aulas/Portal > Tareas, ajustar según contexto).

### 2. Por cada gap

1. Si es endpoint sin smoke: agregar test que llama `GET /api/<modulo>/ping` y verifica 200 + payload.
2. Si es componente React sin render test: TBD — necesita test runner configurado (Vitest).
3. Si es service sin tests: agregar happy path + 1-2 edge cases.

### 3. Mantener cobertura significativa

No perseguir coverage por coverage. **Priorizar BR-\* y caminos críticos** sobre cubrir cada getter.

## Verificación

1. Tests nuevos pasan.
2. Tests existentes siguen pasando (`dotnet test` completo).
3. Si lane business: el BR-\* tiene entry en Test mapping.
4. Si lane technical: el módulo cubierto tiene smoke endpoint y al menos un test del happy path.

## Abrir PR

Ver [`open-pr.md`](./open-pr.md). Título sugerido: `test: cover BR-<modulo>-NNN` o `test: smoke for <modulo>`.
