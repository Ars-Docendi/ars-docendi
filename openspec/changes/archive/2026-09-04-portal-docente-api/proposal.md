## Why

El Portal Docente sigue funcionando con un `mockStore` en memoria, por lo que los datos se pierden y ningún otro consumidor puede consultar el perfil. El módulo `Modules.Portal` ya está incorporado al Host y tiene persistencia/migración preparada, así que este es el siguiente paso para convertir la pantalla implementada en una funcionalidad real.

## What Changes

- Crear el modelo persistente del perfil docente en el schema `portal`, manteniendo identidad, DNI, legajo y CUIL en `identity`.
- Implementar endpoints autenticados para consultar el perfil propio y actualizar independientemente contacto, CV, experiencia, educación, certificaciones, proyectos, habilidades e intereses.
- Implementar contratos DTO/interfaz públicos del módulo Portal y la separación Controller -> Service -> Repository.
- Reemplazar el consumo del `mockStore` del frontend por React Query y `apiClient`, conservando la UX existente.
- Registrar metadata de archivos PDF y DOI, sin almacenamiento binario en este cambio.
- Agregar migraciones SQL versionadas, auditoría, seed sintético idempotente y documentación de API/modelo/dominio.
- Agregar pruebas de API, persistencia, autorización y adaptación frontend.

## Capabilities

### New Capabilities

- `portal-docente-api`: consulta y autogestión persistente del perfil docente, incluyendo sus secciones y tags.

### Modified Capabilities

Ninguna. La integración frontend, el seed y las migraciones son consecuencias de implementación de la nueva capacidad Portal; no cambian por sí mismas el contrato de esas capacidades existentes.

## Impact

- **Backend:** `Modules.Portal`, `Modules.Portal.Contracts`, Host/DI y pruebas.
- **Frontend:** `features/portal` y sus hooks/API; se retira el uso productivo del `mockStore`.
- **Base de datos:** nuevas tablas y auditoría en `portal`; referencias blandas a `identity.personas` según la política existente.
- **Documentación:** `api-contracts`, `data-model`, `domains/portal`, `dependency-graph` y el inventario de mocks.
- **Consumidores cross-module:** no se agrega una dependencia nueva; Portal continúa siendo hoja del DAG. Los contratos quedan disponibles para futuros consumidores de lectura.
- **Rollback:** revertir el cambio y ejecutar una migración anterior; el seed es idempotente y no elimina filas ajenas. La metadata de adjuntos no implica archivos que deban recuperarse.
