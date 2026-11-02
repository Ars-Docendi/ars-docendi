## Why

El rol Administrador de Sistemas (`sys_admin`) no dispone de una vista operativa para distinguir fallas de la API de problemas de conectividad con PostgreSQL, ni de una pantalla para consultar la auditoría sin acceso directo a la base. Incorporar ambas consultas con permisos explícitos mejora el diagnóstico y la trazabilidad sin convertir el log —que contiene snapshots potencialmente sensibles— en una exposición indiscriminada de datos.

## What Changes

- Agregar un dashboard que muestre el estado actual de los pings de los módulos API y la conectividad de PostgreSQL, con hora y duración de cada comprobación.
- Agregar una pantalla de solo lectura para consultar `audit.change_log`, con filtros y paginación del lado del servidor.
- Proteger ambas superficies en frontend y backend con permisos dedicados, concedidos inicialmente a `sys_admin`.
- Mostrar metadatos de auditoría y un detalle seguro de campos modificados; no exponer snapshots JSON sin procesar ni `client_ip`.
- Limitar el dashboard a estado actual: no incorporar tendencias, retención, alertas ni métricas históricas en esta entrega.

## Capabilities

### New Capabilities

- `administracion-sistema`: estado operativo para Administrador de Sistemas y consulta protegida, paginada y de solo lectura de los eventos de auditoría.

### Modified Capabilities

## Impact

- Frontend React: nuevas rutas y páginas administrativas integradas a la navegación por permisos.
- Backend ASP.NET Core: consultas GET protegidas para estado del sistema y eventos de auditoría; la lectura de auditoría usa la persistencia existente de `identity`/`audit`.
- Autorización: nuevos códigos en el catálogo cerrado, políticas del backend y membresía inicial explícita para `sys_admin` mediante SQL versionado.
- Documentación de contratos API y modelo de datos. No se agregan dependencias externas ni tablas nuevas; la auditoría conserva su retención actual.
