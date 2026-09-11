## Why

La exportación actual genera un XLSX técnico de dos hojas que no coincide con la
planilla institucional utilizada para tramitar designaciones. Esto obliga a
rearmar manualmente la información y deja fijos en el encabezado períodos que
ya no representan la configuración vigente del sistema.

## What Changes

- Reemplazar las hojas actuales por las tres hojas del modelo institucional:
  `PROPUESTA COMPLETA`, `ALTAS` y `BAJAS`, conservando su estructura visual,
  encabezados combinados, estilos y columnas.
- Parametrizar los textos de período usando la configuración vigente:
  - primera hoja, columna D: período inmediatamente anterior;
  - primera hoja, columna E: período activo;
  - hojas `ALTAS` y `BAJAS`: período activo.
- Representar en la primera hoja la propuesta completa por docente y materia,
  diferenciando cargo/dedicación anterior y propuesta vigente, y conservar el
  tratamiento de continuidades, cambios, altas y bajas ya materializados.
- Separar los pedidos finalizados de Alta y Baja en sus hojas respectivas,
  incluyendo CUIL y los datos personales disponibles para altas.
- Consultar el correo del docente mediante `Modules.Portal.Contracts`; dejar
  vacíos los campos del modelo para los que el sistema no tiene una fuente
  persistida confiable, sin inventar valores.
- Mantener la ruta, autorización, período activo, lectura consistente,
  protección de textos y tipado de números/fechas del exportador vigente.
- Actualizar los tests del XLSX y la documentación de API, arquitectura y
  dominio para reflejar el nuevo libro.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `exportacion-lote-designaciones`: cambia la estructura y el contenido visible
  del XLSX exportado, sus reglas de parametrización de período y la información
  de contacto incluida en la hoja de altas.

## Impact

- **Backend:** `Modules.Designaciones`, especialmente el repositorio y servicio
  de lote, el escritor XLSX y sus tests de integración.
- **Dependencia:** `Modules.Designaciones` incorporará una referencia a
  `Modules.Portal.Contracts` y resolverá `IPortalQueries` por DI. No se
  referenciará la implementación interna de Portal y el grafo seguirá siendo
  acíclico.
- **API HTTP:** no cambia la ruta ni el tipo de respuesta; el mismo endpoint
  devolverá el libro institucional de tres hojas.
- **Datos:** no requiere cambios de schema. Los valores desconocidos se
  conservan vacíos; no se agregan columnas para completar datos que no existen.
- **Documentación:** se actualizarán `api-contracts-designaciones.md`,
  `domains/designaciones.md`, `dependency-graph.md` y la spec vigente de
  exportación.
- **Rollback:** volver a desplegar conjuntamente la versión anterior de
  backend/frontend restaura el XLSX de dos hojas. La dependencia de Portal sólo
  se usará durante la generación y no modifica datos ni requiere migración.
