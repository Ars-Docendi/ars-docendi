# Design

## Context

Motivación: ver `proposal.md`. Comportamiento: ver `specs/`.

Estado actual relevante:

- **Catálogo de dedicaciones.** `designaciones.dedicaciones` restringe `codigo` a 1–6 (`dedicaciones_codigo_check`, baseline `001`) y está sembrado con las Categorías 1 a 6 (`007`). `orden` es único.
- **Dedicación de texto.** `designaciones.designaciones.dedicacion` y `designaciones.pedidos.dedicacion_solicitada` son texto con `CHECK` sobre "Categoría 0" a "Categoría 6". Hoy nada las escribe:
  - el trigger `validar_dedicacion_pedido` rechaza un texto nuevo;
  - `validar_dedicacion_vigente` exige `dedicacion_id` activo en toda designación nueva o modificada;
  - el seed sintético usa sólo los id.

  El backend las lee como respaldo (`DedicacionCatalogo?.Nombre ?? Dedicacion`) en lote, consultas y administración.

- **Cargo.** `designaciones.cargo_id` es `NOT NULL`, con FK a `cargos`. En pedidos, `cargo_solicitado_id` ya es nullable. `pedidos_completitud_circuito` (migración `20261013173721`) exige cargo, dedicación y horas a un Alta o Cambio en circuito.
- **Sellos de integridad.** `CanonicoSellos.HashEstado` serializa el pedido completo, incluidos `dedicacion_solicitada` (texto) y el snapshot. Hay que adaptarlo a las columnas nuevas. El sistema todavía no está en producción, así que no hay pedidos sellados que preservar.
- **Sin datos previos.** No hay ambientes productivos con datos. El diseño se adapta al modelo nuevo y no contempla compatibilidad con datos anteriores.
- **Administración de personas.** `ServicioAdministracionDesignaciones.ReemplazarVigentesAsync` toma las designaciones deseadas que envía el editor y cierra las que no vienen. Si una designación externa no se puede representar en el editor, guardar la persona la cerraría.
- **Asistente.** Concede `SELECT` por columna (`database/asistente/001`) y clasifica cada columna en `manifiesto-privilegios.json`; un test falla ante columnas sin clasificar. Sus lecturas hacen `JOIN designaciones.cargos` obligatorio (`CompiladorDePlan`, `BuscadorDeMenciones`), así que hoy dejarían afuera a un externo.

## Goals / Non-Goals

**Goals:**

- Una sola representación de la dedicación: el catálogo, con la Categoría 0 incluida.
- Designaciones y pedidos externos con coherencia garantizada por la base, no sólo por el backend.
- Un único cambio de esquema, como migración incremental del modelo actual: no se editan el baseline ni las migraciones publicadas.

**Non-Goals:**

- Completar "cargo/dedicación departamental" y "horas destinadas al ingreso" del lote: no hay una fuente persistida equivalente y siguen vacías.
- Agrupar o filtrar por sector en el tablero de revisión. Sólo se muestra.
- Administrar el catálogo de sectores desde la UI. Como con los cargos, se mantiene por SQL versionado.

## Decisions

### 1. Externo = FK no nula, sin columna booleana

`sector_externo_id uuid NULL` con FK a `designaciones.sectores_externos`, en `designaciones` y en `pedidos`. Que el registro sea externo se deduce de `sector_externo_id IS NOT NULL`.

Alternativa descartada: `externo boolean` más la FK. Son dos datos que dicen lo mismo y se pueden contradecir (`externo = true` sin sector, o un sector con `externo = false`), lo que obligaría a otro `CHECK` sólo para mantenerlos alineados.

### 2. Coherencia en la base

- **`designaciones`**: `cargo_id` pasa a nullable y se agrega este `CHECK`:
  ```sql
  CHECK ((sector_externo_id IS NULL AND cargo_id IS NOT NULL AND dedicacion_id IS NOT NULL)
      OR (sector_externo_id IS NOT NULL AND cargo_id IS NULL AND dedicacion_id IS NULL))
  ```
- **`pedidos`**: un pedido no exige cargo mientras es borrador o si es una Baja, así que el `CHECK` sólo prohíbe mezclar:
  ```sql
  CHECK (sector_externo_id IS NULL OR (cargo_solicitado_id IS NULL AND dedicacion_solicitada_id IS NULL))
  ```
  Además, `pedidos_completitud_circuito` se reemplaza para aceptar `(cargo y dedicación) o sector`, siempre con horas positivas.
- **Triggers**: `validar_dedicacion_vigente` y `validar_dedicacion_pedido` se reescriben con `CREATE OR REPLACE` en la migración nueva. Si hay sector externo, no exigen dedicación; si no lo hay, siguen exigiendo una dedicación del catálogo activa. Un sector nuevo o modificado se valida activo, igual que la dedicación.

### 3. Dedicación de texto sin uso, sin eliminar columnas

El cambio sólo agrega y modifica. Las columnas `designaciones.dedicacion` y `pedidos.dedicacion_solicitada` quedan en la base, pero el backend deja de mapearlas y leerlas, y los triggers siguen rechazando cualquier texto nuevo. La dedicación queda así sólo en el catálogo.

Alternativa descartada: eliminarlas. Obligaría a editar `database/asistente/001_asistente_grants.sql`, que concede `SELECT` sobre esas columnas y está protegido por el manifiesto de migraciones.

### 4. Categoría 0 en el catálogo

`ALTER TABLE ... DROP CONSTRAINT dedicaciones_codigo_check` y se agrega un `CHECK (codigo BETWEEN 0 AND 6)`. Se inserta `('d6000000-0000-4000-8000-000000000000', 0, 'Categoría 0', orden 0, activo)` con `ON CONFLICT DO NOTHING`. `orden = 0` respeta la unicidad y la ubica primera.

### 5. Sellos adaptados al modelo nuevo

`VersionPayload` pasa de `v1` a `v2`: `HashEstado` deja de escribir `dedicacion_solicitada` y suma `sector_externo_id`. El snapshot sellado suma `sector`. Como no hay pedidos sellados, el formato canónico se adapta directamente, sin mecanismos de compatibilidad. Los tests de integridad existentes cubren que un pedido sellado verifica y que una alteración se detecta, también para un pedido externo.

### 6. Snapshot y datos actuales

`SnapshotPedido` suma `SectorExterno`, con la sigla. Para un externo, `Cargo` y `Dedicacion` quedan nulos. Los DTO de designación vigente y de pedido exponen `sectorExternoId` y el nombre y código del sector.

En `Modules.Designaciones.Contracts` los DTO de designación vigente usan `CargoId` y `DedicacionId` nullables y suman `SectorExternoId`. El consumidor (`ArsDocendi.Host/Administracion`) se actualiza en el mismo diff y no se agregan aristas.

### 7. Validación de pedidos y materialización

`ValidacionCircuitoPedidos`, la creación y la edición aplican la misma regla que el `CHECK`, devolviendo un error por campo (`sectorExternoId`, `cargoSolicitadoId` o `dedicacionSolicitadaId`) antes de llegar a la base. `MaterializadorDesignaciones` copia al aprobar el sector, o el cargo y la dedicación, y limpia lo que no corresponde. Por eso un Cambio puede convertir una designación interna en externa y al revés.

### 8. Lote

Una función única arma la celda: `CargoDedicacion(cargo, dedicacion, sectorExterno)` devuelve la sigla cuando hay sector y "cargo / dedicación" cuando no. Se aplica en la propuesta, las altas, las bajas y la designación anterior, que sale del snapshot. En `PROPUESTA COMPLETA`, la columna Departamento lleva el nombre completo del sector de una designación vigente o un Alta externa, y "Departamento de Ingeniería e Investigaciones Tecnológicas" en una interna; en una baja queda vacía, como la propuesta.

Las tres hojas agrupan por docente (`AgrupadorLote`): una fila por docente y combinación de cargo/dedicación, con los códigos de materia unidos por guion, las horas sumadas y las observaciones y números de pedido distintos combinados. Si un docente tuviera combinaciones distintas, cada una queda en su fila para no mezclar valores.

### 9. Frontend

- **`SeccionDesignacionSolicitada`**: suma un interruptor "Docente externo". Al activarlo, cargo y dedicación se vacían y se deshabilitan, y aparece el selector de sector con los sectores activos del catálogo. `pedidoValidacion.ts` replica la regla.
- **`DatosActualesPanel`, `ResumenPedido` y el detalle**: muestran "Externo – {nombre} ({código})".
- **`features/personas/AsignacionesPersona`**: suma el mismo interruptor por fila.

### 10. Asistente

Una migración del contexto Asistente concede `SELECT` sobre `designaciones.sectores_externos`, con la misma clasificación que `cargos`, y sobre las columnas `sector_externo_id`, y actualiza el manifiesto. `CompiladorDePlan` y `BuscadorDeMenciones` pasan a `LEFT JOIN cargos` y exponen el sector. `COMMENT ON` describe la tabla y las columnas nuevas.

### 11. Designaciones sólo por pedido

- **Personas:** `GuardarPersonaDto` deja de traer designaciones y el Host deja de llamar al reemplazo de designaciones; `IAdministracionDesignaciones` pierde `ValidarReemplazoAsync` y `ReemplazarVigentesAsync`, que quedan sin uso. El editor muestra las designaciones en modo lectura. Se quita la regla "rol Docente o Jefe de Cátedra exige al menos una asignación".
- **Cargadores:** `MaquinaEstadosPedido` define quién carga: Jefe de Cátedra de la materia, Administrativo o Secretaría. Crear, editar y eliminar borradores, enviar y corregir una devolución al primer nivel usan esa regla. El historial registra el rol con el que actuó. Una migración de Identity otorga `designaciones.gestionar` a Administrativo y Secretaría.
- **Alta de persona existente:** endpoint `GET /api/designaciones/personas/buscar?texto=` (permiso `designaciones.gestionar`) que devuelve hasta 20 personas sin pedido vivo en el período activo. El backend ya admitía un Alta con `personaId`. El frontend suma un pop-up con el patrón de `PopupBuscarResponsable`.
- **Specs de alta y edición de docentes:** `crear-docente` y `editar-docente` describen la edición de asignaciones; los está modificando el change sin archivar `administracion-unificada-personas`. Para no superponer deltas, la regla vigente queda en `designaciones-solo-por-pedido` y esos dos specs se concilian al archivar ambos changes.

## Risks / Trade-offs

- **[Guardar una persona cierra una designación externa]** → El editor de personas soporta externos, y el backend rechaza una designación deseada que mezcle sector con cargo o dedicación.
- **[Consumidores que asumen cargo no nulo]** (lote, asistente, fichas del Host) → Se relevaron con búsquedas de `CargoId`, `cargo_id`, `Cargo.` y `Dedicacion`, y se ajustan en el mismo diff. Las pruebas de integración cubren un externo en cada lectura afectada.
- **[Archivos grandes]** `ServicioLoteDesignaciones` y `PedidoForm` ya son extensos → Las reglas nuevas van en funciones o componentes chicos (por ejemplo, `SelectorSectorExterno`) para respetar el tope de ~300 líneas.

## Migration Plan

1. Migración del contexto Designaciones, generada con `node scripts/migrations/scaffold.mjs Designaciones DedicacionCatalogoYExternos`:
   - catálogo 0–6 e insertar la Categoría 0;
   - crear `sectores_externos`, sembrarla y adjuntarle auditoría;
   - agregar las columnas `sector_externo_id`, los `CHECK` y la nueva `pedidos_completitud_circuito`;
   - reescribir los triggers;
2. Migración del contexto Asistente para permisos y comentarios, y actualización del manifiesto.
3. Desplegar la aplicación. Los scripts de carga inicial (fuera del repo) vuelven a incluir la Categoría 0 y los externos con `sector_externo_id`.

**Rollback:** revertir la versión de la aplicación. La migración no revierte DDL: las columnas y la tabla nuevas quedan sin uso. Las columnas de texto no se tocan. Si hiciera falta volver atrás el esquema, se hace con una migración incremental nueva.
