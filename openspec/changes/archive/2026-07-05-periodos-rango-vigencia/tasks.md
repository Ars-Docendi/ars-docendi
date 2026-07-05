## 1. Modelo de datos

- [x] 1.1 Actualizar `PeriodoDesignacion` en `types.ts`: eliminar `cuatrimestre`, `anio`, `estado` (3 valores); agregar `cargaDesde`, `cargaHasta`, `impactoDesde`, `impactoHasta` (renombre de `fechaApertura`/`fechaCierre`), `activo: boolean`
- [x] 1.2 Eliminar el tipo `EstadoPeriodo` si queda sin otros usos (y corregir consumidores directos: `PedidoFormPage.tsx`, `MisPedidosPage.tsx`, que usaban `anio`/`cuatrimestre`/`estado==="abierto"`)

## 2. Validación de fechas (red-green)

- [x] 2.1 Escribir test que falla para `validarPeriodo` en `periodoValidacion.test.ts`: casos `cargaHasta < cargaDesde` e `impactoHasta < impactoDesde` deben devolver error
- [x] 2.2 Implementar `periodoValidacion.ts` con la función pura `validarPeriodo` (mismo patrón que `pedidoValidacion.ts`) hasta que el test pase
- [x] 2.3 Agregar test de caso válido (fechas coherentes, sin errores)

## 3. Modal de creación/edición (`ModalPeriodo.tsx`)

- [x] 3.1 Reemplazar los campos del formulario por: Nombre (Input, vacío por defecto), Carga desde (DatePicker), Carga hasta (DatePicker), Impacto desde (DatePicker), Impacto hasta (DatePicker)
- [x] 3.2 Quitar los campos Cuatrimestre, Año y el Select de Estado del modal
- [x] 3.3 Implementar la sugerencia: al completar "Impacto desde", si "Carga hasta" está vacío, pre-completar con un mes antes de "Impacto desde" (editable)
- [x] 3.4 Conectar `validarPeriodo` al guardar: bloquear submit y mostrar error inline por campo (`Field error=`) si hay error de rango de fechas
- [x] 3.5 Ajustar `periodoAFormulario` / estado inicial del formulario al nuevo modelo

## 4. Tabla de períodos y control de activación

- [x] 4.1 Actualizar `TablaPeriodos.tsx`: columnas Nombre, Carga desde, Carga hasta, Impacto desde (mes/año), Impacto hasta (mes/año), Activo, Acciones
- [x] 4.2 ~~Agregar columna con `Toggle`~~ **Revertido dos veces**: 1) la columna Activo quedó de solo lectura (texto "Activo"/"Inactivo"); 2) el botón "Activar/Desactivar período" en el footer del modal (intento intermedio) también se revirtió — ver 4.2b
- [x] 4.2b Agregar `Toggle` "Período activo" **como campo del formulario** (`ModalPeriodo.tsx`, debajo de Nombre), disponible en creación y edición — no un botón de acción separado, se confirma junto con el resto de los datos al hacer clic en "Guardar"
- [x] 4.3 Eliminar `EstadoPeriodoPill.tsx` (ya no hay 3 estados) — confirmado con grep que solo lo usaba `TablaPeriodos.tsx` (el `EstadoPedidoPill.tsx` de Pedidos es otro componente, no se toca)
- [x] 4.4 Crear `ModalDesactivarPeriodo.tsx` para confirmar la desactivación, siguiendo el patrón de `ModalEliminarPeriodo.tsx` (no se reusa `ModalConfirmacionAccion`, acoplado a Pedidos). Se dispara desde `ModalPeriodo` únicamente cuando la transición es `activo: true → false` al guardar; el modal de edición se cierra antes de abrir la confirmación
- [x] 4.5 Mover la validación de "único período activo" a `periodoValidacion.ts` (`validarPeriodo` recibe `ContextoValidacionPeriodo` con el resto de los períodos): si el `Toggle` queda en `true` con otro período ya activo, error de campo bajo el `Toggle`, sin guardar — rechazo duro, sin auto-swap
- [x] 4.6 Wiring en `PeriodosPage.tsx`: `handleGuardar` unificado para crear/editar/activar/desactivar; `handleNecesitaConfirmarDesactivacion` intercepta la transición a inactivo y guarda los datos pendientes hasta confirmar

## 5. Mock data

- [x] 5.1 Regenerar `periodosMock.ts` completo al modelo nuevo (`cargaDesde/cargaHasta/impactoDesde/impactoHasta/activo`)
- [x] 5.2 Asegurar que exactamente un período del mock tenga `activo: true` (id "1", igual que antes — preserva `PERIODO_ABIERTO_ID` en `pedidosSeed.ts`) y el resto `false`
- [x] 5.3 Mantener variedad de ventanas de carga/impacto para representar un historial realista

## 6. Documentación y specs

- [x] 6.1 Verificar que la delta spec de `gestion-periodos` (ya generada en este change) sincroniza correctamente al archivar
- [x] 6.2 Revisar `docs/architecture/domains/designaciones.md`: no describe el modelo de `PeriodoDesignacion` (confirmado con grep) — sin cambios necesarios

## 7. Verificación

- [x] 7.1 Correr suite de tests del feature: 16/16 archivos, 107/107 tests OK (incluye 3 tests nuevos de la regla "único período activo" en `periodoValidacion.test.ts`); `tsc -b` sin errores
- [x] 7.2 Levantado el frontend y validado manualmente en `/designaciones/periodos`: campos del modal (Nombre, Toggle "Período activo", 4 fechas), sugerencia de `cargaHasta`, validación de fechas y de único activo (bloquea guardar + mensajes por campo), desactivar un período activo (pide confirmación al guardar), activar sin conflicto (guarda directo)
- [x] 7.3 Confirmado: solo componentes de `@ars-docendi/ui` (`Modal`, `Field`, `Input`, `DatePicker`, `InlineAlert`, `Button`, `Table.*`, `Toggle`, `Select`) — sin componentes custom nuevos de UI primitiva
