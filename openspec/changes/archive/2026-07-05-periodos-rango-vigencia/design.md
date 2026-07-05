## Context

El feature de gestión de períodos de designación (`frontend/src/features/designaciones/`, capability `gestion-periodos`) vive hoy enteramente sobre mock data (`periodosMock.ts`) — no hay backend ni persistencia real todavía. El modelo actual (`PeriodoDesignacion`) mezcla en un único rango de fechas (`fechaApertura`/`fechaCierre`) dos conceptos que Secretaría Académica maneja por separado en la práctica: la ventana donde el Jefe de Cátedra puede cargar pedidos, y el período real donde esas designaciones impactan (ej. 2do cuatrimestre = agosto-diciembre). El estado de 3 valores (`abierto`/`próximo`/`cerrado`) tampoco refleja la realidad operativa: hoy la apertura/cierre es siempre una decisión manual de Secretaría, nunca una transición automática por fecha.

Este design cubre exclusivamente el frontend del módulo Designaciones — no hay componente backend (`Modules.Designaciones`) involucrado en períodos aún, por lo que no aplican las reglas de fronteras cross-module ni el grafo de dependencias del backend.

## Goals / Non-Goals

**Goals:**

- Separar la ventana de carga (`cargaDesde`/`cargaHasta`) del rango de impacto (`impactoDesde`/`impactoHasta`) como dos conceptos independientes en el modelo.
- Simplificar el estado del período a un booleano (`activo`), con control manual explícito (no derivado de fechas).
- Garantizar que nunca haya 2 períodos activos simultáneamente (regla bloqueante, enforced en la UI).
- Convertir el límite de carga (`cargaHasta`) en un límite blando: se sigue permitiendo cargar, con advertencia, hasta que Secretaría desactive el período manualmente.
- Usar exclusivamente componentes ya existentes de `@ars-docendi/ui` — cero componentes custom nuevos.

**Non-Goals:**

- No se implementa backend/persistencia real para períodos en este change — se mantiene sobre mock data, igual que hoy.
- No se agrega un componente de selección de solo mes/año al design system — se usa `DatePicker` de día completo y se trunca visualmente. Pedir un `MonthPicker` a `ui-lib` queda anotado como idea futura, fuera de alcance.
- No se derivan reportes ni filtros por año/cuatrimestre en este change — solo se remueven esos campos del modelo.
- No se define aquí el comportamiento del lado del Jefe de Cátedra al cargar un pedido fuera de la ventana de carga (la advertencia puntual que ve el docente) — se deja el enganche a `pedidoValidacion.ts` para un change posterior si hace falta; este change solo agrega el dato y la regla a nivel período.

## Decisions

### 1. Estado binario (`activo: boolean`) en vez de 3 valores

Se elimina `EstadoPeriodo` (`abierto`/`próximo`/`cerrado`) y se reemplaza por `activo: boolean`. **Alternativa considerada**: mantener 3 valores y derivar "próximo" de las fechas de impacto. Se descarta porque el estado nunca fue derivado automáticamente en la práctica (era un `Select` manual en el modal) y "próximo" no tiene una definición operativa clara con la regla de "un único período activo, apagado/encendido a mano".

### 2. `Toggle` "Período activo" como campo del formulario (no un botón de acción)

**Segunda corrección sobre versiones anteriores de este design.** Se planteó primero un `Toggle` en columna propia de `TablaPeriodos`; después, un botón "Activar/Desactivar período" en el footer del modal de edición; antes de eso, reusar `ModalConfirmacionAccion` (descartado por acoplarse a `PedidoDesignacion`). Ninguna de las dos primeras convencía: la columna en la grilla mezclaba lectura y mutación en la tabla, y el botón de acción sonaba raro al crear ("activar" algo que todavía no existe).

La versión final trata `activo` como **un campo más del formulario** (`Toggle` "Período activo", al final del formulario, después de la ventana de carga y el período de impacto), disponible tanto al crear como al editar — no es una acción con su propio botón, es un atributo que se define junto con el nombre y las fechas y se confirma con el mismo "Guardar". Va al final a propósito: para cuando el usuario llega ahí ya completó nombre y fechas, así que "marcar como activo" es una decisión sobre un período ya definido, no sobre algo a medio llenar. La grilla (`TablaPeriodos`) sigue mostrando el estado activo/inactivo como texto de solo lectura.

Comportamiento al hacer clic en "Guardar" (crear o editar):

- La validación de "único período activo" corre dentro de `validarPeriodo` (recibe el resto de los períodos como contexto). Si el `Toggle` quedó en `true` y ya existe otro período activo (distinto del que se está editando), se bloquea el guardado y se muestra el error bajo el propio campo `Toggle` — mismo mecanismo que el resto de los errores de validación (por campo, no un `InlineAlert` genérico de página). Rechazo duro, sin auto-swap.
- Si el período **ya estaba activo** y el `Toggle` se apaga, en vez de guardar directo se dispara `onNecesitaConfirmarDesactivacion`: se cierra `ModalPeriodo` y se abre `ModalDesactivarPeriodo` (mismo patrón que `ModalEliminarPeriodo.tsx`) con los datos ya validados pendientes; al confirmar, se aplica el guardado completo (incluye cualquier otro cambio hecho en el mismo formulario, no solo el campo `activo`).
- En cualquier otro caso (crear con `activo` en cualquier valor, o editar sin pasar de activo a inactivo) se guarda directo.

### 3. Rechazo duro (no auto-swap) ante 2 períodos activos

Si Secretaría intenta activar un período B mientras A sigue activo, el sistema bloquea con un mensaje inline (ej. "Ya existe un período activo: <nombre>. Desactivalo primero."). **Alternativa considerada**: swap automático (desactivar A y activar B con una confirmación). Se descarta porque desactivar un período tiene efecto directo sobre lo que ven los docentes (deja de aceptar carga sin advertencia de por medio) — un swap implícito dentro de la acción de "activar otro período" es una superficie de error mayor a la de simplemente pedir que el usuario lo haga en dos pasos explícitos.

### 4. `DatePicker` de día completo para el rango de impacto (no mes/año nativo)

`@ars-docendi/ui` no tiene una variante de `DatePicker` limitada a mes/año — el `type` del input está bloqueado a `"date"`. Se usa el mismo `DatePicker` que ya existe para los 4 campos de fecha (`cargaDesde`, `cargaHasta`, `impactoDesde`, `impactoHasta`), y en `TablaPeriodos` se formatea la visualización de `impactoDesde`/`impactoHasta` truncando a "Mes Año" (ej. "Agosto 2026"). **Alternativa considerada**: dos `Select` (Mes + Año), igual al patrón que hoy usa Cuatrimestre/Año. Se descarta por ahora para no multiplicar campos en el formulario; queda como alternativa a revisar si la ambigüedad de "elegir un día que no importa" genera confusión real en el uso.

### 5. Sugerencia de `cargaHasta` no forzada

Al completar `impactoDesde`, si `cargaHasta` está vacío, se pre-completa con la fecha correspondiente a un mes antes de `impactoDesde` (mismo día del mes anterior). El campo sigue siendo editable — es un valor por defecto, no una validación ni un límite.

### 6. Validación de fechas en módulo dedicado

Se crea `periodoValidacion.ts` con una función pura `validarPeriodo`, siguiendo el mismo patrón que `pedidoValidacion.ts` (ya existente en el feature): `cargaHasta >= cargaDesde` y `impactoHasta >= impactoDesde`. Se cubre con test rojo-verde antes del fix, según la disciplina de bug/validación del proyecto.

### 7. Reglas operativas internas, no `BR-*`

La regla de "único período activo" y la de "alerta no bloqueante pasada `cargaHasta`" son decisiones operativas de Secretaría Académica, no normativa institucional citable (no hay artículo de reglamento detrás) — quedan documentadas acá y no generan entradas en `docs/business-rules/designaciones.md`.

## Risks / Trade-offs

- **[Riesgo] Cambio de forma del tipo `PeriodoDesignacion` es breaking** → Mitigación: no hay backend/persistencia real todavía (todo es mock data), así que no hay migración de datos ni contrato de API que romper fuera del propio frontend.
- **[Riesgo] `DatePicker` de día completo para impacto puede confundir al usuario** (¿por qué elijo un día si solo importa el mes?) → Mitigación: se documenta como decisión revisable (ver Decisión 4); si en uso real genera fricción, se reemplaza por 2 `Select` sin tocar el resto del modelo.
- **[Riesgo] Eliminar `EstadoPeriodoPill` rompe cualquier otro lugar que lo importe** → Mitigación: es un componente interno del feature (`designaciones/components/`), no se re-exporta fuera de la feature; se verifica con grep antes de eliminar.
- **[Trade-off] Rechazo duro (no swap) agrega un paso manual extra** para pasar de un período activo a otro → aceptado deliberadamente por menor riesgo de desactivación accidental (ver Decisión 3).

## Migration Plan

No aplica migración de datos: el feature no tiene persistencia backend, se regenera `periodosMock.ts` directamente al nuevo modelo como parte de este mismo PR. Rollback es un revert simple del PR.

## Open Questions

- ¿El límite blando de `cargaHasta` (alerta sin bloqueo) debe reflejarse también del lado del formulario de carga de pedidos (`PedidoForm.tsx` / `pedidoValidacion.ts`)? Este change solo agrega el dato y la semántica a nivel período; el enganche con la carga de pedidos se evalúa en un change posterior si Secretaría lo pide.
- ¿Vale la pena pedir un `MonthPicker` a `ui-lib` a futuro para el rango de impacto? Queda anotado como idea, no como tarea de este change.
