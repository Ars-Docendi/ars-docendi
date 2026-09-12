## Why

Las pantallas administrativas quedaron desalineadas con el diseño visual vigente: Usuarios aún
renderiza una columna `Ámbitos`, el editor de usuarios muestra un SVG de calendario no solicitado
y Roles usa tamaños tipográficos propios en lugar de la escala compartida. El ajuste corrige esa
deriva visual sin cambiar contratos, datos ni flujos de administración.

## What Changes

- Eliminar la columna visible `Ámbitos` de la tabla de Usuarios, conservando las membresías para
  filtros y edición.
- Reemplazar el `DatePicker` del formulario de edición de usuarios por un control nativo de fecha
  sin el SVG `.cal-ico`, manteniendo el valor, la validación y el payload actuales.
- Alinear la tipografía y los tamaños de la pantalla Roles con los tokens y controles usados por
  Usuarios y Docentes.
- Actualizar las specs delta y los design specs administrativos para documentar la presentación
  resultante.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `listar-usuarios`: la tabla visible no debe mostrar la columna `Ámbitos`, aunque la API y el
  editor puedan seguir usando las membresías.
- `listar-roles`: la pantalla debe usar la familia tipográfica y la escala de tamaños compartidas
  por las pantallas administrativas de Usuarios y Docentes.
- `modificar-rol-usuario`: el campo de fecha de nacimiento del editor debe conservar el control
  nativo sin un SVG de calendario adicional.

## Impact

- **Frontend:** `features/usuarios/components/TablaUsuarios.tsx`, el editor de usuarios y
  `features/roles/roles.css`, más sus pruebas de presentación.
- **Documentación:** specs delta del cambio y los design specs
  `administracion-usuarios-docentes` y `administracion-roles-permisos`.
- **API, base de datos y dependencias:** sin cambios; no se modifica el grafo de dependencias.
- **Rollback:** revertir los cambios de componentes, estilos, pruebas y documentación. No requiere
  migraciones ni coordinación con backend.
