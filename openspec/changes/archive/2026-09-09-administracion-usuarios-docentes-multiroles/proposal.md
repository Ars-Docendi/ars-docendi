## Why

La administración de Usuarios y Docentes no representa correctamente que una membresía de rol puede estar acotada a una materia. El caso visible de Gustavo Ruiz muestra tres asignaciones legítimas de `jefe_catedra` como tres roles repetidos y, al editar, el frontend puede reenviar esas asignaciones como duplicadas. Esto vuelve frágiles las ediciones válidas, oculta la relación entre cuentas y docentes y dificulta distinguir conflictos de concurrencia de errores de datos.

## What Changes

- Corregir el flujo `PUT /api/administracion/usuarios/{id}` para que una edición válida de datos o roles conserve la semántica de concurrencia optimista y distinga conflictos de versión, UPN, documento y legajo.
- Representar roles de usuario como un resumen sin duplicados y conservar por separado todas las membresías con su ámbito de materia o carrera.
- Permitir expresar explícitamente que una misma cuenta es `docente` en una materia y `jefe_catedra` en otra, sin generar el producto cartesiano de todos los roles con todas las materias.
- Alinear la edición de Usuarios y Docentes con las asignaciones `rol + ámbito` persistidas en `identity.user_roles`.
- Hacer visible en Usuarios qué cuentas tienen perfil docente, cuántas materias abarcan y cómo llegar a su información docente; mostrar en Docentes si existe una cuenta vinculada y cómo volver a ella.
- Redibujar los iconos de Usuarios, Docentes, Roles y Membresía de Roles con un único estilo SVG legible y accesible, sin incorporar una dependencia nueva.
- Actualizar el contrato API, las especificaciones funcionales, la documentación de arquitectura y el design spec administrativo afectados por la nueva representación.

## Capabilities

### New Capabilities

- `administracion-membresias-ambito`: edición y visualización explícita de membresías de roles con ámbito por materia o carrera.
- `relacion-usuarios-docentes`: identificación, navegación y estado de cuenta de la relación entre usuarios y docentes.
- `iconografia-administracion`: iconografía consistente y legible para las cuatro entradas administrativas.

### Modified Capabilities

- `administracion-identidad-api`: el contrato debe distinguir roles resumidos de asignaciones completas y documentar los conflictos del PUT.
- `modificar-rol-usuario`: la edición debe enviar y conservar membresías con ámbito, sin usar nombres de rol como identificadores.
- `listar-usuarios`: el listado debe mostrar roles sin duplicar y hacer visible el perfil docente.
- `crear-docente`: el alta debe admitir membresías docentes por materia sin asumir un único rol global.
- `editar-docente`: la edición debe permitir roles docentes diferentes según la materia.
- `listar-docentes`: el listado debe mostrar la relación con la cuenta y los roles docentes resumidos sin duplicados.

## Impact

- **Backend:** `ArsDocendi.Shared.Identity.Administracion`, `ArsDocendi.Host.Administracion` y los controladores administrativos. Se reutilizan las constraints existentes de `identity.user_roles`; no se propone duplicar cuentas ni crear un booleano persistido `es_docente`.
- **Frontend:** features `usuarios`, `docentes` y shell de navegación. Los formularios deberán conservar IDs canónicos de rol, materia y carrera, no etiquetas visibles.
- **API y documentación:** cambia la forma de representar las membresías de usuario y la información de perfil docente; se deben actualizar [api-contracts-administracion.md](../../../docs/architecture/api-contracts-administracion.md), las specs relacionadas y el design spec administrativo.
- **Dependencias:** la composición de un resumen docente puede leer designaciones sólo mediante `Modules.Designaciones.Contracts`; no se agregan referencias a implementaciones internas de otro módulo ni se altera el grafo DAG.
- **Regla pendiente:** se documenta como requisito funcional la coexistencia de roles por materia. Sólo se registrará como `BR-<modulo>-NNN` cuando Secretaría/cliente confirme la fuente normativa institucional.
- **Rollback:** desplegar frontend y backend como conjunto compatible; si la nueva forma de lectura no puede consumirse, volver a la versión anterior de ambos sin eliminar filas de `identity.user_roles`. No hay migración destructiva prevista.
