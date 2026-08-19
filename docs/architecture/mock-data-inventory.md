# Inventario de datos mock del frontend

Este inventario clasifica las fuentes alcanzables desde el runtime. La migración a API elimina sólo registros y catálogos persistidos; constantes visuales, reglas cerradas y fixtures exclusivas de tests permanecen donde corresponda.

## Datos persistidos que deben salir del runtime

| Fuente                                                 | Consumidores                                 | Autoridad final                                              |
| ------------------------------------------------------ | -------------------------------------------- | ------------------------------------------------------------ |
| `features/usuarios/mock/mockStore.ts`                  | `/usuarios`, filtros y modales               | `identity.personas`, `identity.users`, `identity.user_roles` |
| `features/docentes/mock/mockStore.ts`                  | `/docentes`, filtros, selectores y modales   | `identity` + `designaciones.designaciones`                   |
| `features/roles/mock/mockStore.ts`                     | `/roles`, configuración compartida           | `identity.roles`                                             |
| `features/membresia-roles/mock/mockStore.ts`           | `/membresia-roles`, configuración compartida | `identity.permisos`, `identity.rol_permisos`                 |
| `features/designaciones/api/pedidosSeed.ts`            | store local de pedidos                       | `designaciones.pedidos`, adjuntos e historial                |
| `features/designaciones/api/pedidosStore.ts`           | seam `pedidosApi`                            | API de Designaciones                                         |
| `features/designaciones/api/periodosMock.ts`           | períodos, formularios, detalle y listas      | `designaciones.periodos`                                     |
| registros de `features/designaciones/api/catalogos.ts` | formulario y datos actuales                  | cargos, personas, materias y designaciones vigentes          |
| `shared/auth/dev/mockUsers.ts`                         | login y cambio de rol                        | identidades sintéticas consultadas al backend                |

`shared/configuracion/ConfiguracionContext.tsx` no contiene fixtures propias, pero mantiene copias mutables de roles y membresías. También debe desaparecer como fuente de estado remoto.

## Preferencias locales admitidas

La sesión de desarrollo MAY persistir únicamente el identificador de usuario y el código de rol elegidos. No guarda nombres, permisos, ámbitos ni registros de negocio; el backend vuelve a validarlos en cada solicitud.

Las claves históricas `adoc.dev.mockUser`, `adoc.dev.mockRol` y `adoc.mock.pedidos.v4` se retiran. Sus reemplazos de desarrollo usan nombres que no impliquen datos mock y nunca son autoridad de autorización.

## Constantes que permanecen en código

- etiquetas, tonos, iconos y textos de estados o novedades;
- configuración de columnas, filtros y tamaños de página;
- reglas cerradas ya modeladas por tipos, como novedades, dedicaciones y tipos de baja;
- helpers puros de formato, normalización y cálculo presentacional;
- estado efímero de formularios, modales, filtros, pestañas y navegación.

Una constante deja de pertenecer a esta categoría si representa una fila identificable de una tabla o si un operador puede modificarla mediante una pantalla.

## Fixtures exclusivas de tests

Los tests MAY construir personas, usuarios, pedidos o respuestas HTTP locales. Esas fixtures deben vivir en archivos `*.test.*`, `__fixtures__` o utilidades importadas sólo desde tests. Ningún entrypoint de runtime puede alcanzarlas.

## Criterio automatizable

La validación final falla si código runtime:

- importa desde un directorio `/mock/`;
- importa `pedidosSeed`, `pedidosStore`, `periodosMock` o datasets equivalentes;
- lee o escribe la clave de pedidos mock en `localStorage`;
- incorpora un array de objetos que reproduce filas de usuarios, docentes, roles, permisos, materias, cargos, períodos o pedidos.
