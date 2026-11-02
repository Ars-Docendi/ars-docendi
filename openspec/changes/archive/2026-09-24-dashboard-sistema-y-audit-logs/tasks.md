## 1. Autorización y permisos

- [x] 1.1 Agregar `sistema.estado.ver` y `auditoria.ver` al catálogo C# y a sus políticas; verificar políticas backend y nombres entregados por la API de permisos.
- [x] 1.2 Crear SQL versionado idempotente para insertar ambos permisos y asignarlos explícitamente a `sys_admin`; verificar migración en base limpia y en una instalación ya migrada, sin alterar otras membresías.

## 2. Estado operativo

- [x] 2.1 Implementar consulta protegida de estado PostgreSQL con consulta mínima, timeout y respuesta sanitizada; verificar estados disponible/no disponible, duración y ausencia de secretos en errores.
- [x] 2.2 Integrar los cuatro pings existentes y el estado de PostgreSQL en el dashboard; verificar que una falla parcial no oculte los estados restantes y que hora/estados desconocidos se representen correctamente.
- [x] 2.3 Cubrir la autorización de la API del dashboard para `sistema.estado.ver`; verificar que un usuario sin permiso recibe denegación.

## 3. Consulta de auditoría

- [x] 3.1 Implementar consulta GET paginada de `audit.change_log` con filtros parametrizados, orden estable y límite máximo; verificar filtros, rango inválido, páginas límite y resultados vacíos.
- [x] 3.2 Implementar DTO/detalle con metadatos y política de valores aprobados/enmascarados; verificar PII, secretos, valores de campos desconocidos, actor nulo y ausencia de `client_ip`/snapshots JSON crudos.
- [x] 3.3 Cubrir permiso `auditoria.ver` y garantizar que la superficie no ofrece mutaciones; verificar denegación sin permiso y que consultar no cambia eventos.

## 4. Interfaz administrativa

- [x] 4.1 Agregar feature, rutas y enlaces de navegación por permisos para dashboard y auditoría; verificar enlace visible sólo con permiso y bloqueo de acceso directo sin él.
- [x] 4.2 Implementar estados accesibles de carga, éxito, vacío y error recuperable, actualización del dashboard, filtros y paginación; verificar comportamiento con respuestas API exitosas y fallidas.
- [x] 4.3 Agregar pruebas de interfaz para estado parcial, filtros/paginación y detalle con valores enmascarados; ejecutar las pruebas frontend relevantes.

## 5. Documentación y verificación integrada

- [x] 5.1 Actualizar contratos API, modelo de datos y documentación de arquitectura/autorización para endpoints, permisos, límites de consulta y redacción; verificar consistencia con implementación y OpenSpec.
- [x] 5.2 Ejecutar pruebas backend/frontend, lint, build, `git diff --check` y `openspec validate dashboard-sistema-y-audit-logs --strict`; registrar resultados y preservar cambios ajenos.
