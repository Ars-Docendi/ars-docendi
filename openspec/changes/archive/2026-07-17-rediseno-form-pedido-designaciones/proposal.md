## Why

El cliente (Jefe de Cátedra real, sesión de exploración `/opsx:explore` del 2026-07-05) pidió un lote de
cambios sobre el form de pedido de designaciones: hoy el pedido solo captura **una** materia con cargo y
dedicación "solicitados" a nivel de esa materia, no permite cargar horas de investigación ni horas
externas, y confunde la semántica de "depto externo" con un toggle binario en vez de un dato numérico.
Estos gaps ya fueron mockeados en `docs/product/designs/screens.pen` (frames `n1zz2M`, `tZANr`, `JOHDw`)
y documentados en `docs/product/designs/rediseno-designaciones-exploracion.md`; este change los baja a
código para los tipos de novedad Alta, Baja y Cambio.

## What Changes

- **Modelo de horas del pedido**: se agregan horas de investigación y horas externas (otro
  departamento) como campos numéricos libres, sin validación de cierre contra la dedicación (D2).
- **Múltiples materias en Alta y en Cambio** (D3): un pedido pasa de una única `materiaAsociada` a
  una lista de asignaciones (materia + horas), agregable y quitable (mínimo 1 materia obligatoria) en
  ambas novedades, manteniendo "un pedido por docente por período" (no reabre BR-designaciones-001,
  solo cambia la cardinalidad de materias dentro del mismo pedido). En Cambio, la lista se precarga
  con las materias que ya tiene el docente. **BREAKING**: cambia la forma de
  `PedidoDesignacion.materiaAsociada` (string) a una lista de asignaciones; todo lector del campo
  (tabla, card, resumen, adapters de detalle) se actualiza en el mismo change.
- **"Modificar horas docente" dentro del pedido**: el form expone la lista de materia + horas editable
  (agregar/quitar/seleccionar materia, editar horas) tanto en Alta como en Cambio, distinto de la
  futura pantalla Datos Docente (fuera de alcance).
- **Cargo y dedicación como atributos únicos del docente** (D1): se confirma como ya está el
  modelo de datos actual (`DocenteExistente.cargoActual` / `.dedicacionActual` son de la persona, no
  de la materia) — no hay cambio estructural, se documenta como invariante del capability.
- **Cargo solicitado sin restricción de jerarquía**: en Cambio, el cargo es una selección libre entre
  todos los valores del catálogo — no hay restricción de "solo hacia arriba". Esa restricción
  (jerarquía de cargos) es el tema C, explícitamente fuera de alcance de este change.
- **Dedicación solicitada: solo puede mejorar** (revisión posterior del cliente, revierte la lectura
  original del doc de exploración): en Cambio, `dedicacionSolicitada` debe ser jerárquicamente mejor
  que la actual — Categoría 0 es la de mayor jerarquía, Categoría 6 la de menor. El `Select` filtra
  las opciones disponibles y `pedidoValidacion.ts` lo refuerza como defensa en profundidad.
- **Resumen de cambios en el recuadro gris de datos actuales (Cambio)**: el panel muestra la
  transición `actual → solicitado` de cargo, dedicación, cada materia (con sus horas) y horas de
  investigación/externas — no solo dedicación como antes. El catálogo mock (`DocenteExistente`) suma
  `horasInvestigacionActuales`/`horasExternasActuales` para tener con qué comparar.
- **Eliminación del toggle "hace más horas en otro Departamento"** (`haceHorasOtroDepto`),
  reemplazado por el campo numérico explícito **Horas externas**. **BREAKING**: se elimina el campo
  booleano `haceHorasOtroDepto` de `PedidoDesignacion` / `DatosEditablesPedido`.
- **Tipificación de la Baja**: nuevo campo "Tipo de baja" (enum cerrado: Renuncia / Jubilación / Otro,
  con texto libre para "Otro"), obligatorio antes de "Motivo de la baja".
- **Catálogo de Dedicación**: se extiende de 6 a 7 valores (agrega "Categoría 0") en
  `frontend/src/features/designaciones/api/catalogos.ts`.

No forma parte de este change (quedan para changes futuros, ver
`docs/product/designs/rediseno-designaciones-exploracion.md` → Mapa de temas): jerarquía de cargos y
"quitar prioritario" (tema C), rediseño de la pantalla Revisión (tema E), Historial de pedidos (tema F),
restricción real de período abierto (tema G), pantalla Datos Docente, y la reconciliación de
`AsignacionMateria.cargo` de `admin-docentes` con D1 (queda como deuda técnica en
`docs/quality/tech-debt.md`).

## Capabilities

### New Capabilities

_(ninguna — este change extiende un capability existente)_

### Modified Capabilities

- `pedidos-designacion`: cambian los requisitos de "Creación de pedido de designación" (múltiples
  materias con horas en vez de una sola materia asociada) y de "Secciones condicionales por novedad"
  (nuevos campos de horas de investigación/externas, tipo de baja en Baja, y el toggle de depto
  externo se retira en favor del campo numérico).

## Impact

- **Frontend** (`frontend/src/features/designaciones/`): `types.ts` (modelo `PedidoDesignacion` /
  `DatosEditablesPedido`), `api/catalogos.ts` (catálogo Dedicación 0–6), `api/pedidosSeed.ts` (datos
  mock), `pedidoValidacion.ts` (+tests), `components/PedidoForm.tsx`,
  `components/SeccionDesignacionSolicitada.tsx`, `components/SeccionDocentePedido.tsx`,
  `components/DatosActualesPanel.tsx`, y todo lector de `materiaAsociada`/`haceHorasOtroDepto`
  (`TablaMisPedidos.tsx`, `TablaRevision.tsx`, `PedidoCard.tsx`, `ResumenPedido.tsx`,
  `detalleAdapters.ts`, `tableroRevisionModelo.ts`) + sus tests.
- **Sin impacto en backend**: el flujo de designaciones sigue siendo un prototipo frontend-only con
  store mock (`pedidosStore.ts` + `localStorage`); no hay módulo `.NET` ni schema de base de datos
  afectado por este change.
- **Sin impacto cross-module vía Contracts**: no se toca ningún `Modules.*.Contracts`.
- **Docs**: `docs/product/designs/proyecto-docente-design-spec.md` (si documenta el form vigente) y
  `docs/product/designs/rediseno-designaciones-exploracion.md` (marcar temas A+B+D como implementados
  al cerrar el change).
- **Rollback**: cambio acotado a un feature branch del monorepo frontend, sin migraciones de datos
  (mock/localStorage); revertir el PR restaura el comportamiento anterior sin efectos secundarios.
