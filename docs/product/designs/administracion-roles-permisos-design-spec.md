# Design spec — Administración de Roles y Permisos

## Alcance

`/roles` es la pantalla canónica para consultar y administrar roles activos y sus permisos. La
ruta histórica `/membresia-roles` redirige a `/roles` y no renderiza una segunda pantalla.

## Layout

- `/roles`: grilla administrativa (`Table` de la guía, como Personas) con columnas Nombre,
  Descripción, Ámbito, Tipo (Sistema o Personalizado), Permisos (`asignados/total`), Estado y
  Acciones (Editar y, en personalizados, Desactivar o Activar). Filtros por encabezado, orden por
  columna y fila clickeable que abre el rol. Lista activos e inactivos.
- `/roles/nuevo` y `/roles/:rolId`: pantalla completa con Nombre, Descripción y Ámbito arriba y,
  debajo y sin encabezado propio, los permisos agrupados por módulo. Pie con Cancelar y Guardar.
- Cada módulo es un bloque desplegable, cerrado por defecto, con casilla de módulo (marcada,
  desmarcada o intermedia), nombre, contador `asignados/total` y chevron. Al desplegarlo lista sus
  permisos con casilla, nombre y descripción. El módulo sale del prefijo del código del permiso.
- Un buscador sobre los módulos filtra por nombre o descripción del permiso (o nombre del módulo):
  quedan solo los módulos con coincidencias, desplegados. Casilla y contador siguen refiriéndose
  al módulo completo.
- Estados de carga y error reintentable; las acciones sin permiso no se renderizan.

## Tipografía

La pantalla reutiliza la familia tipográfica institucional y la misma escala de tamaños base,
secundarios y de controles que las pantallas de Usuarios y Docentes. La jerarquía del título del
rol, las descripciones, los metadatos y las acciones conserva esa escala sin tamaños propios de la
pantalla.

## Reglas de edición

- `roles.ver` permite consultar la lista y los permisos.
- `roles.administrar` habilita crear, editar, desactivar y reactivar roles personalizados.
- `roles.gestionar_membresia` habilita marcar permisos y guardarlos para cualquier rol activo.
- Un rol personalizado exige nombre único y mantiene su código al renombrarse. Un rol nuevo arranca
  con todos los permisos destildados.
- Un rol de sistema muestra código, nombre, descripción, ámbito y estado como solo lectura. No se
  ofrece eliminarlo; sus permisos sí pueden editarse.
- Desactivar y Activar piden confirmación. Desactivar deja el rol inactivo en la grilla, conservando
  asignaciones y permisos históricos.

## Accesibilidad y estados

Los módulos usan `<details>`/`<summary>`, operables con teclado; la casilla de módulo tiene nombre
accesible («Todos los permisos de <módulo>») y no pliega el bloque. Las casillas se deshabilitan sin
`roles.gestionar_membresia` o con el guardado pendiente. Los conflictos de concurrencia mantienen el contexto seleccionado y
requieren recargar la versión vigente; no se muestran datos ajenos ni mensajes técnicos.
