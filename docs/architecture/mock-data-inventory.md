# Datos locales del frontend

El runtime obtiene personas, usuarios, docentes, roles, permisos, materias, cargos, períodos, pedidos y perfiles desde las APIs. Ningún array TypeScript, store en memoria ni `localStorage` es autoridad para esos registros.

## Preferencias locales admitidas

La autenticación de desarrollo puede guardar únicamente el identificador del usuario y el código de rol elegidos. El backend vuelve a resolver la cuenta, sus permisos y su ámbito en cada solicitud.

El estado efímero de formularios, modales, filtros y navegación también permanece en el cliente.

## Constantes admitidas

Pueden vivir en código:

- etiquetas, tonos, íconos y textos de estados;
- configuración de columnas, filtros y paginación;
- vocabularios cerrados modelados por tipos;
- helpers puros de formato o presentación.

Una constante deja de pertenecer a esta categoría si representa una fila identificable o si un operador puede modificarla.

## Fixtures de tests

Los tests pueden construir respuestas HTTP y entidades locales dentro de archivos `*.test.*` o utilidades importadas sólo por tests. Ningún entrypoint de runtime puede alcanzarlas.

El build ejecuta `frontend/scripts/check-no-runtime-mocks.mjs` y falla si el runtime importa directorios `/mock/` o las fuentes locales de negocio retiradas.
