# Design spec — Administración de Roles y Permisos

## Alcance

`/roles` es la pantalla canónica para consultar y administrar roles activos y sus permisos. La
ruta histórica `/membresia-roles` redirige a `/roles` y no renderiza una segunda pantalla.

## Layout

- Panel izquierdo: buscador por nombre o descripción y lista de roles activos.
- Panel derecho: nombre, tipo de rol, cantidad de permisos, catálogo completo de permisos y
  estado de guardado.
- La vista muestra un estado de carga, error reintentable y un placeholder cuando no hay un rol
  seleccionado. Los grupos y acciones sin permiso no se renderizan.

## Reglas de edición

- `roles.ver` permite consultar la lista y los permisos.
- `roles.administrar` habilita crear, editar roles personalizados y solicitar su baja lógica.
- `roles.gestionar_membresia` habilita marcar permisos y guardarlos para cualquier rol activo.
- Un rol personalizado exige nombre único, mantiene su código al renombrarse y puede usar otro rol
  activo como base para copiar permisos una sola vez.
- Un rol de sistema muestra código, nombre, descripción, ámbito y estado como solo lectura. No se
  ofrece eliminarlo; sus permisos sí pueden editarse.
- La baja pide confirmación y deja el rol fuera de la lista y de nuevos roles base, conservando
  asignaciones y permisos históricos.

## Accesibilidad y estados

Los controles de selección son botones o checkboxes nativos con nombre accesible. El panel de
permisos deshabilita los checkboxes y el guardado mientras no exista `roles.gestionar_membresia` o
la mutación esté pendiente. Los conflictos de concurrencia mantienen el contexto seleccionado y
requieren recargar la versión vigente; no se muestran datos ajenos ni mensajes técnicos.
