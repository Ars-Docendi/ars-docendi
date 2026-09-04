## Why

Las pantallas funcionales consumen datos hardcodeados, estado en memoria o `localStorage`, mientras que el seed no productivo sólo registra metadata y el backend expone únicamente endpoints de diagnóstico. Esto impide validar el sistema de punta a punta, hace que las distintas pantallas representen universos de datos inconsistentes y oculta errores de persistencia, autorización y contratos HTTP antes de llegar a producción.

## What Changes

- Convertir el seed sintético no productivo en la fuente autoritativa de datos de ejemplo para `identity` y `designaciones`, con registros coherentes, idempotentes y suficientes para recorrer estados normales, vacíos y excepcionales de la UI.
- Exponer APIs HTTP reales para usuarios, docentes, roles, membresías de permisos, catálogos de identidad y el ciclo completo de pedidos y períodos de designación.
- Reemplazar en runtime los stores mock, fixtures TypeScript y persistencia en `localStorage` por el cliente Axios compartido y hooks de React Query, conservando fixtures aisladas sólo dentro de tests.
- Incorporar estados de carga, vacío, error, invalidación y refresco de consultas en las pantallas migradas.
- Mantener constantes puramente presentacionales o reglas cerradas de UI en el frontend; no se consideran datos mock los textos, etiquetas, configuraciones visuales ni mapeos de estados.
- Incorporar un flujo de suplantación para desarrollo que obtiene de la API las identidades sembradas y sus ámbitos. El flujo estará disponible únicamente en ambientes no productivos y no se registrará en producción.
- Actualizar contratos y documentación de API, modelo de datos, dominios y grafo de dependencias junto con las nuevas superficies públicas.
- Adaptar el alcance frontend-only de los cambios activos `admin-docentes` y `roles-membresia`: sus pantallas pasarán a consumir estas APIs en vez de stores locales.

## Capabilities

### New Capabilities

- `datos-ejemplo-no-productivos`: Siembra sintética, segura, coherente e idempotente para ambientes no productivos.
- `administracion-identidad-api`: API persistente para administrar usuarios, personas docentes, roles, permisos, asignaciones y catálogos de identidad.
- `sesion-desarrollo-sembrada`: Selección segura, sólo fuera de producción, de una identidad sembrada con roles y ámbitos resueltos por el backend.
- `integracion-frontend-api`: Requisitos transversales para que las features consuman la API real, manejen el estado remoto y no incluyan datos mock en runtime.

### Modified Capabilities

- `listar-usuarios`: El listado se obtiene desde la persistencia canónica mediante la API y refleja cambios durables.
- `crear-usuario`: El alta se valida y persiste en backend, actualizando las consultas del frontend.
- `desactivar-usuario`: La activación y desactivación se ejecutan en backend y se reflejan entre sesiones.
- `modificar-rol-usuario`: Las asignaciones de rol y sus ámbitos se administran mediante la API persistente.
- `pedidos-designacion`: La creación, consulta, edición, envío, reenvío y eliminación de pedidos dejan el store local y operan sobre la persistencia del módulo.
- `aprobacion-pedidos-designacion`: Las acciones del circuito de revisión se autorizan, ejecutan y auditan en backend.
- `gestion-periodos`: Los períodos se consultan y administran mediante endpoints persistentes en vez de fixtures TypeScript.

## Impact

- **Backend:** nuevas superficies Controller → Service → Repository para administración de identidad y `Modules.Designaciones`; DTOs públicos en los Contracts correspondientes cuando haya consumo cross-module.
- **Frontend:** `shared/api`, autenticación de desarrollo y features `usuarios`, `docentes`, `roles`, `membresia-roles` y `designaciones`; React Query pasa a ser la única fuente de estado remoto.
- **Base de datos e infraestructura:** ampliación de `infra/scripts/seed-data/sintetico.sql` sobre los schemas existentes, sin datos derivados de producción y conservando el bloqueo de siembra insegura.
- **Consumidores cross-module:** Designaciones seguirá leyendo identidad mediante el contrato público existente; la administración será la única superficie que escriba personas, roles y permisos. No se agregan referencias a internals de otros módulos.
- **Grafo de dependencias:** se conserva el DAG y no se introducen ciclos; se documentará la dependencia de Designaciones hacia consultas públicas de identidad y la superficie administrativa transversal.
- **Compatibilidad:** las rutas de frontend se conservan. Los seams mock internos dejan de estar disponibles en runtime; los tests deberán usar adapters HTTP simulados o fixtures propias.
- **Rollback:** el seed se limita a ambientes no productivos y será reejecutable. La migración frontend se realizará por feature, permitiendo revertir cada adapter API durante el despliegue; no se eliminarán columnas ni datos de negocio. El endpoint de suplantación tendrá registro condicional y una prueba que garantice su ausencia en producción.
- **Normativa:** el cambio implementa infraestructura y comportamiento ya especificado; no introduce nuevas reglas provenientes de normativa institucional.
