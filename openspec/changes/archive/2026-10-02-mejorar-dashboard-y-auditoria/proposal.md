## Why

La pantalla de auditoría muestra el UUID del actor y etiquetas técnicas como `schema.tabla`, por lo que cuesta responder rápidamente quién hizo qué y en qué módulo. Además, el botón de actualización y los estilos locales del dashboard no reutilizan consistentemente los componentes y tokens visuales del sistema.

## What Changes

- Alinear el dashboard con el design system, reservar el estilo primario para acciones principales y presentar **Actualizar** como acción secundaria.
- Reorganizar la auditoría para que lo primero visible sea **fecha, usuario, acción, módulo y resumen del cambio**, con el objeto y la clave como contexto y valores seguros en el detalle.
- Enriquecer los eventos con un nombre de actor legible, una etiqueta de módulo y un resumen comprensible, manteniendo los filtros, la paginación y el orden estable.
- Resolver el actor a través de la identidad disponible; cuando no se pueda identificar, indicarlo explícitamente. No exponer UPN, correo, `client_ip` ni snapshots crudos.
- Agregar una especificación UX de estas pantallas y actualizar el contrato API y la documentación afectados.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `administracion-sistema`: la lista de auditoría presenta actor, acción, módulo y resumen del evento de forma comprensible y segura, conservando consulta paginada de solo lectura.

## Impact

- Frontend: páginas del dashboard y auditoría, usando los componentes y tokens visuales compartidos.
- Backend: consulta y DTO de auditoría para nombre de actor, módulo y resumen; joins de lectura dentro de la infraestructura `identity`/`audit`.
- Documentación UX y contratos API. No se requieren cambios de schema, permisos, retención ni dependencias externas; la auditoría sigue siendo de solo lectura.
