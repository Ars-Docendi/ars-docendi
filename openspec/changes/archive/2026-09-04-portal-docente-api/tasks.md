## 1. Persistencia y contratos

- [x] 1.1 Crear el DDL versionado de `portal` para perfil, contacto, CV, experiencias, educación, certificaciones, proyectos, documentos y tags, con `created_at`, restricciones y auditoría; verificar que el SQL sea válido y que cada tabla tenga `audit.attach`.
- [x] 1.2 Incorporar las entidades necesarias a `PortalDbContext` sin que EF intente recrear el DDL SQL; verificar que no haya cambios de modelo pendientes.
- [x] 1.3 Agregar DTOs de request/response e interfaces públicas en `Modules.Portal.Contracts`; verificar que Contracts no contenga lógica ni referencias a infraestructura.
- [x] 1.4 Crear repositorios y servicio de Portal con resolución del usuario autenticado mediante `IConsultasIdentity`; verificar aislamiento por `persona_id` con pruebas de acceso propio/ajeno.

## 2. API del Portal

- [x] 2.1 Implementar `GET /api/portal/perfil` y el mapeo agregado de todas las secciones; verificar respuestas completa, vacía y no autenticada.
- [x] 2.2 Implementar actualización independiente de contacto y CV, incluyendo validación de mail, PDF metadata, reemplazo y eliminación; verificar HTTP 400/200 y persistencia selectiva.
- [x] 2.3 Implementar CRUD de experiencia, educación, certificaciones y proyectos; verificar creación 201, edición 200, eliminación 204, validaciones y rechazo de recursos ajenos.
- [x] 2.4 Implementar reemplazo independiente de habilidades/intereses, normalización, sugerencias y eliminación por tipo; verificar que ambas listas permanezcan independientes.
- [x] 2.5 Aplicar autorización y respuestas problem details a todos los endpoints; verificar 401, 404/403 según la convención común y que no se filtren datos ajenos.

## 3. Migración, seed y documentación

- [x] 3.1 Agregar los SQL de Portal a la migración embebida y al orden de ejecución del módulo; verificar `dotnet run --project backend/src/ArsDocendi.Host -- --migrate` sobre una base limpia.
- [x] 3.2 Extender `infra/scripts/seed-data/sintetico.sql` con UUIDs estables para perfiles completo, parcial y vacío, metadata de archivos y tags sugeridos; verificar reejecución sin duplicados ni pérdida de filas ajenas.
- [x] 3.3 Actualizar `docs/architecture/api-contracts.md`, `data-model.md`, `domains/portal.md`, `dependency-graph.md` y `mock-data-inventory.md`; verificar que documenten los endpoints, tablas, seed y ausencia de nuevos edges.

## 4. Integración frontend

- [x] 4.1 Crear el adaptador `features/portal/api` con `apiClient` y tipos compatibles con la pantalla; verificar pruebas de rutas, payloads y mapeos.
- [x] 4.2 Reemplazar el hook y las mutaciones del `mockStore` por React Query, manteniendo estados de carga/error/éxito e invalidación tras guardar; verificar los tests existentes de la pantalla adaptados a API.
- [x] 4.3 Retirar las importaciones productivas del mock y conservarlo solo donde sea necesario para tests; verificar con `rg` que `/portal` no dependa de `mockStore`.

## 5. Verificación integral

- [x] 5.1 Ejecutar la suite backend y frontend, smoke tests de `ping` y pruebas de integración autenticadas; verificar que todo pase y que el perfil seed se vea desde las identidades de desarrollo.
- [x] 5.2 Verificar cap de archivos, logging estructurado, límites de autorización y ausencia de bytes de archivos almacenados; registrar cualquier deuda explícita en `docs/quality/tech-debt.md`.
