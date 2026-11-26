# Proposal

## Why

La carga inicial de producción con la propuesta de designación septiembre–diciembre 2026 no puede representar a 49 docentes con Categoría 0 ni a 30 docentes externos. El catálogo de dedicaciones sólo admite códigos 1 a 6 (`dedicaciones_codigo_check`), y toda designación nueva exige cargo y una dedicación activa de ese catálogo (trigger `validar_dedicacion_vigente`). Por eso la Categoría 0 sobrevive sólo como texto histórico, en una columna paralela con su propio `CHECK`. Un docente externo es el que trabaja más horas en otro departamento o área de la universidad, que le paga; dicta clases en Ingeniería pero no tiene cargo ni dedicación propios del Departamento, y hoy el sistema no tiene cómo registrarlo.

## What Changes

- La dedicación se elige **sólo del catálogo** `designaciones.dedicaciones`, que suma la Categoría 0 (rango 0–6). Las columnas de texto `designaciones.dedicacion` y `pedidos.dedicacion_solicitada` quedan sin uso: nada las escribe y el sistema deja de leerlas. No se eliminan, para que el cambio sólo agregue y modifique.
- Nuevo catálogo `designaciones.sectores_externos` (código, nombre, activo), sembrado con los nueve sectores de la propuesta: ACA, DPU, ECO, ARQ, SEU, DCP, CSA, CYT y HUM.
- Designaciones y pedidos suman una referencia opcional al sector externo. Un registro es **interno**, con cargo y dedicación del catálogo y sin sector, o **externo**, con sector y sin cargo ni dedicación. La base garantiza que no haya mezclas. Cargo y dedicación pasan a admitir nulo sólo para el caso externo.
- **Alta y Cambio de designación:** opción "Docente externo" que deshabilita cargo y dedicación y muestra un selector de sector externo. Las horas se siguen pidiendo. El backend valida lo mismo.
- **Administración de personas:** las designaciones vigentes pasan a ser de sólo lectura (se ven, internas o externas, pero no se editan) y la API de personas deja de aceptarlas. Así la designación sólo cambia por pedido y no puede divergir de lo que muestra el circuito. El alta de un docente ya no exige designación: la crea el pedido de Alta aprobado.
- **Carga de pedidos:** además del Jefe de Cátedra, Administrativo y Secretaría cargan pedidos de cualquier materia, con el mismo circuito; si se devuelve al primer nivel, lo corrige el Jefe o un Administrativo/Secretaría. Se les otorga `designaciones.gestionar`.
- **Alta de persona existente:** buscador emergente (nombre, apellido, DNI o legajo) para elegir una persona del sistema, como alternativa a una persona nueva.
- **Exportación del lote:** para un externo, la celda de cargo/dedicación lleva la sigla del sector (por ejemplo, `ECO`), como en la planilla institucional. Además, `PROPUESTA COMPLETA`, `ALTAS` y `BAJAS` pasan a tener un registro por docente: sus materias juntas (códigos unidos por guion) y las horas sumadas, porque el cargo y la dedicación son únicos por docente.
- Los datos actuales del docente y el snapshot del pedido muestran el sector cuando la designación es externa.
- El asistente incluye las designaciones externas en sus consultas y conoce el catálogo de sectores.

## Capabilities

### New Capabilities

- `docentes-externos`: catálogo de sectores externos y designaciones externas de punta a punta. Cubre la coherencia interno/externo en la base, la selección en Alta, Cambio y en la administración de personas, su visualización y su representación en el lote.

- `designaciones-solo-por-pedido`: designaciones de sólo lectura en la administración de personas, carga de pedidos por Administrativo y Secretaría, y Alta de una persona existente.

### Modified Capabilities

- `persistencia-designaciones`: el catálogo de dedicaciones pasa a 0–6 y es la única forma de dedicación; la designación vigente admite cargo y dedicación nulos sólo cuando es externa.
- `pedidos-designacion`: las dedicaciones seleccionables pasan a ser 0 a 6; la completitud de Alta y Cambio exige cargo y dedicación, o sector externo.
- `pedidos-designacion` también: edición y eliminación por Administrativo y Secretaría.
- `exportacion-lote-designaciones`: la celda de cargo/dedicación de un externo muestra la sigla del sector.

## Impact

- **Base de datos:** una migración incremental del contexto Designaciones (`database/designaciones/`) y una del contexto Asistente para permisos y clasificación de las columnas y la tabla nuevas. No se editan baselines ni migraciones publicadas.
- **Backend:** `Modules.Designaciones` (entidades, catálogos, validación de pedidos, materialización, administración de designaciones, lote, sellos de integridad), `Modules.Designaciones.Contracts` (DTO de designación vigente consumidos por `ArsDocendi.Host/Administracion`) y `Modules.Asistente` (lecturas de designaciones).
- **Consumidores cross-module:** `ArsDocendi.Host` (administración de personas) consume `IAdministracionDesignaciones` y `IDesignacionesQueries`; sus DTO suman el sector externo y cargo y dedicación nullables. No se agregan aristas al grafo de dependencias.
- **Frontend:** `features/designaciones` (formulario de pedido, datos actuales, resumen y detalle) y `features/personas` (editor de designaciones vigentes).
- **API:** el catálogo de Designaciones suma `sectoresExternos`; pedidos y designaciones vigentes suman `sectorExternoId`. Se documenta en `api-contracts-designaciones.md` y `api-contracts-administracion.md`.
- **Documentación:** `data-model.md`, `domains/designaciones.md` y los artículos del Centro de ayuda de crear y editar pedidos, administrar personas y descargar el lote.
- **Sellos de integridad:** el estado sellado del pedido se adapta a las columnas nuevas. No hay pedidos sellados que preservar, porque el sistema todavía no está en producción.
- **Reglas de negocio:** no proviene de normativa institucional; no se registra BR.
- **Rollback:** la migración no revierte DDL (convención del repo). Revertir la aplicación deja las columnas nuevas sin uso, y los datos externos o de Categoría 0 cargados quedan en la base. Si hiciera falta deshacer el esquema, se hace con una migración incremental nueva.
