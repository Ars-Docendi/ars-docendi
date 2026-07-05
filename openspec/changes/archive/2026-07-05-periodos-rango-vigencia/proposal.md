## Why

El modelo actual de "Período de designación" mezcla en un solo rango de fechas (apertura/cierre) dos cosas distintas: la ventana donde el Jefe de Cátedra carga pedidos de designación, y el período real donde esas designaciones tienen efecto (impacto). Pablo (Secretaría Académica) pidió separar ambos conceptos: hoy carga un alta un mes antes de que el período de impacto empiece (ej. carga en julio para un impacto de agosto a diciembre del 2do cuatrimestre), y el sistema no distingue esa diferencia. Además, el campo `cuatrimestre` quedó redundante frente al nuevo rango de impacto, y el estado de 3 valores (`abierto`/`próximo`/`cerrado`) ya no reflejaba una transición real: hoy la apertura/cierre es una decisión manual de la Secretaría, no algo derivado de fechas.

## What Changes

- Se elimina el campo `cuatrimestre` (1C/2C/Verano) del modelo `PeriodoDesignacion` — queda implícito en el rango de impacto. **BREAKING** (cambio de forma del tipo).
- Se elimina el campo `anio` como campo independiente — se deriva de `impactoDesde` cuando hace falta mostrarlo o filtrar. **BREAKING**.
- El estado de 3 valores (`abierto`/`próximo`/`cerrado`) se simplifica a un booleano `activo`. **BREAKING**. Ya no hay transición automática por fechas: la apertura/cierre es 100% manual.
- Se renombran conceptualmente los campos de fecha existentes: `fechaApertura`/`fechaCierre` → `cargaDesde`/`cargaHasta` (ventana donde el Jefe de Cátedra carga pedidos). **BREAKING** (rename de propiedades del tipo).
- Se agrega un nuevo rango `impactoDesde`/`impactoHasta` — representa el período real de impacto de las designaciones (ej. 2do cuatrimestre = agosto a diciembre), distinto de la ventana de carga. Usa `DatePicker` de día completo (el design system no tiene picker de solo mes/año); en la tabla se muestra truncado a "Mes Año".
- El campo `nombre` (título) se mantiene obligatorio, pero deja de tener valor sugerido — el Jefe de Cátedra lo completa manualmente desde cero.
- Se agrega sugerencia automática no forzada: al completar `impactoDesde`, si `cargaHasta` está vacío, se pre-completa con un mes antes de `impactoDesde` (editable).
- Se agrega un control `Toggle` (activar/desactivar) fuera del modal, en la tabla de períodos, reemplazando el selector de estado que hoy vive dentro del modal. Desactivar pide confirmación (reusa `ModalConfirmacionAccion` ya existente en el feature). Intentar activar un período mientras hay otro activo se rechaza de forma dura (mensaje inline) — **no puede haber 2 períodos activos simultáneamente**.
- Se agrega la semántica de límite blando en `cargaHasta`: pasada esa fecha, el período en sí no bloquea nada — el cierre real solo ocurre cuando la Secretaría desactiva el período manualmente con el `Toggle`. La advertencia puntual que vería el Jefe de Cátedra al cargar un pedido pasado `cargaHasta` queda fuera de alcance de este change (toca `PedidoForm`, fuera del feature de gestión de períodos) — se evalúa en un change posterior.
- Se agrega validación de fechas: `cargaHasta >= cargaDesde` e `impactoHasta >= impactoDesde`, en un módulo `periodoValidacion.ts` (mismo patrón que `pedidoValidacion.ts` existente).
- Se regenera `periodosMock.ts` completo al modelo nuevo (un único período con `activo: true`).

Estas dos reglas de negocio (único período activo, alerta no bloqueante pasada la fecha de carga) son decisiones operativas internas del área, no normativa institucional — no requieren `BR-*`, quedan documentadas en `design.md`.

## Capabilities

### New Capabilities

_(ninguna — este change modifica una capability existente)_

### Modified Capabilities

- `gestion-periodos`: cambia la forma del modelo `PeriodoDesignacion` (elimina `cuatrimestre`/`anio`/estado de 3 valores, agrega rango de impacto y estado binario), cambia el formulario de creación/edición (5 campos: nombre + 4 fechas, sin selector de estado), y agrega el control externo de activar/desactivar con la regla de único período activo.

## Impact

- **Módulo afectado**: Designaciones (frontend únicamente — `frontend/src/features/designaciones/`). No hay componente backend para períodos todavía (el feature vive enteramente sobre mock data), por lo que este change no toca `Modules.Designaciones` ni `Modules.Designaciones.Contracts`, y no hay consumidores cross-module que se vean afectados.
- **Archivos**: `types.ts`, `components/ModalPeriodo.tsx`, `components/TablaPeriodos.tsx`, `components/EstadoPeriodoPill.tsx`, `api/periodosMock.ts`, `pages/PeriodosPage.tsx`, nuevo `periodoValidacion.ts` + test.
- **Spec**: `openspec/specs/gestion-periodos/spec.md` se reescribe (los escenarios de estado "próximo" ya no aplican).
- **Design system**: solo se usan componentes existentes de `@ars-docendi/ui` (`Modal`, `Field`, `Input`, `DatePicker`, `InlineAlert`, `Button`, `Table.*`, `Toggle`). No se crean componentes custom nuevos. Se deja anotada en `design.md` la idea de pedir a futuro un `MonthPicker` al repo `ui-lib`, fuera de alcance de este change.
- **Rollback**: cambio acotado a un feature frontend sin persistencia backend real (mock data) — revertir es un revert de PR simple, sin migración de datos involucrada.
