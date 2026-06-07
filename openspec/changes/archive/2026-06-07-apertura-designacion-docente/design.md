## Context

El módulo de Designaciones tiene actualmente un `IndexPage` placeholder. Este cambio lo reemplaza con la pantalla de gestión de períodos de designación docente, que es el punto de entrada del workflow completo del módulo.

La librería `@ars-docendi/ui` provee todos los primitivos necesarios (Table, Modal, Field, Input, Select, DatePicker, Button, Breadcrumbs, InlineAlert). El tema completo de design tokens ya está instalado vía `theme.css`. El único componente que no existe en la librería es un badge de estado específico del dominio de períodos.

El frontend usa React 19 + TypeScript + Vite. No hay backend disponible en este cambio — toda la data es mock estático.

## Goals / Non-Goals

**Goals:**

- Pantalla funcional visualmente completa con mock data que el cliente puede validar.
- CRUD completo: listar, crear, editar, eliminar períodos.
- Diseño forward-compatible: slots de error listos para validaciones de backend futuras (solapamiento de fechas, eliminación de períodos con pedidos asociados).
- Cero componentes UI nuevos de propósito genérico — todo reutiliza `@ars-docendi/ui`.
- Componente de dominio `EstadoPeriodoBadge` encapsulado en el feature, usando solo tokens del tema.

**Non-Goals:**

- Conexión con backend / persistencia real.
- Validaciones de reglas de negocio (solapamiento, pedidos asociados) — los slots están, la lógica no.
- Cambios en navegación, routing o autenticación.
- Paginación — los períodos son pocos por naturaleza.

## Decisions

### D1: `EstadoPeriodoBadge` como componente de dominio, no primitivo genérico

**Decisión:** Crear `features/designaciones/components/EstadoPeriodoBadge.tsx` en lugar de reutilizar `StatusBadge` de `@ars-docendi/ui` con label override.

**Rationale:** `StatusBadge` tiene kinds hardcodeados de semántica de workflow de expedientes (aprobado, rechazado, devuelto…). Un período de designación tiene su propia semántica de estado (Abierto/Cerrado). Usar `kind="aprobado"` con `label="Abierto"` distorsiona la semántica y crea deuda técnica invisible. Un componente de dominio encapsula el mapping en un solo lugar y no introduce acoplamiento semántico.

**Alternativa descartada:** Contribuir un `Badge` genérico a `ui-lib`. Válido a largo plazo, pero prematuro sin más casos de uso confirmados. Se registra como candidato futuro cuando Aulas y Tareas necesiten badges propios.

**Implementación:** `EstadoPeriodoBadge` usa únicamente variables CSS del tema (`--color-status-success-*`, `--color-status-warning-*`, etc.) — sin valores hardcodeados de color. Si el tema cambia, el badge lo sigue automáticamente.

### D2: Modal para crear/editar, no página dedicada ni Drawer

**Decisión:** Usar `Modal` de `@ars-docendi/ui` para el formulario de alta y edición.

**Rationale:** Un período tiene 5-6 campos simples. Una página dedicada sería sobredimensionada y rompería la sensación de "parametrización rápida". Un `Drawer` lateral tiene sentido cuando el contexto de la lista necesita permanecer visible; aquí no aplica porque editar un período requiere foco total en el formulario.

**Alternativa descartada:** `Drawer` — válido si en el futuro se agrega un panel de detalle del período (historial de pedidos asociados, etc.), pero hoy es innecesario.

### D3: InlineAlert como slot de error, oculto por defecto

**Decisión:** El Modal de edición y el Modal de eliminación incluyen un `InlineAlert` que se renderiza condicionalmente (solo cuando hay un mensaje de error).

**Rationale:** Las reglas de negocio futuras son conocidas de antemano: no permitir dos períodos abiertos con fechas solapadas, no permitir eliminar períodos con pedidos asociados. Diseñar los modales con el slot listo significa que cuando llegue el backend, solo se conecta el estado de error — no se rediseña la UI.

### D4: Mock data como array tipado en archivo separado

**Decisión:** Los datos de prueba viven en `features/designaciones/api/periodosMock.ts`, exportados como `PERIODOS_MOCK: PeriodoDesignacion[]`.

**Rationale:** Separar mock data del componente facilita la futura sustitución por llamadas reales con React Query. El archivo `api/` ya existe en la estructura del feature (con `.gitkeep`). El nombre del export deja claro que es temporal.

**Estructura del mock:** 4 períodos — 1 Abierto (cuatrimestre actual), 2 Cerrados (cuatrimestres anteriores), 1 Próximo (próximo cuatrimestre) — para que el cliente pueda validar los tres estados visuales.

### D5: Estado del período como campo manual en el formulario

**Decisión:** El campo `estado` del período se setea manualmente en el formulario (Select con opciones: Abierto / Cerrado / Próximo).

**Rationale:** En el sistema final, el estado podría derivarse automáticamente de las fechas (si `now` está entre apertura y cierre → Abierto). Sin embargo, para el mock visual es más útil poder controlarlo manualmente. Esta decisión es reversible cuando se integre el backend.

## Risks / Trade-offs

- **Mock data desactualizada:** El array `PERIODOS_MOCK` no refleja la realidad institucional; es solo para validación visual. Riesgo bajo — su propósito es explícito.
  - Mitigación: nombre de archivo y export claramente marcados como `Mock`.

- **`EstadoPeriodoBadge` puede quedar desincronizado con `StatusBadge`:** Si `ui-lib` evoluciona su sistema de badges, el componente de dominio no hereda los cambios.
  - Mitigación: el componente solo consume tokens del tema, no estilos internos de la librería. El riesgo de divergencia visual es mínimo.

- **Modal puede quedarse chico si se agregan campos:** Si el período incorpora campos complejos en el futuro (ej: cargos habilitados, carreras permitidas), el Modal puede resultar insuficiente.
  - Mitigación: escalar a Drawer o página dedicada cuando ocurra. La lógica de estado del formulario ya estará encapsulada en el componente modal, lo que facilita la migración.

## Open Questions

- **¿El estado "Próximo" existe como concepto de negocio?** En explore lo tratamos como un estado visual útil para el mock, pero no está confirmado si el dominio lo necesita formalmente.
- **¿Quién puede gestionar períodos?** Presumiblemente Secretaría Académica, pero el sistema de permisos por módulo está pendiente de definición. Por ahora la pantalla es accesible para cualquier rol autenticado.
- **¿Hay un período "activo" global en el contexto de la app?** El `pretitle` del PageHeader muestra "CUATRIMESTRE 2026 · 1C" hardcodeado; en el futuro debería derivar del período Abierto actual.
