# Design spec — Administración de Usuarios y Docentes

## Alcance

Las pantallas `/usuarios` y `/docentes` muestran la identidad canónica y sus relaciones sin
duplicar cuentas ni persistir un indicador `es_docente`.

## Tablas

- **Usuarios:** apellido y nombre, documento, legajo, UPN, roles resumidos sin duplicados, perfil
  docente con cantidad de materias, estado y acciones. La tabla no muestra una columna `Ámbitos`;
  los ámbitos permanecen disponibles en el editor de membresías.
- **Docentes:** apellido y nombre, documento, legajo, roles resumidos, designaciones, estado y
  estado de cuenta. Un docente con cuenta tiene un enlace `Ver usuario`; uno sin cuenta muestra
  `Sin cuenta`.
- El enlace del perfil docente usa `/docentes?personaId=...`; el de cuenta usa
  `/usuarios?personaId=...`. Ambos abren el registro existente para edición y nunca crean datos.

## Editor de membresías

Cada fila tiene un rol y su ámbito explícito: materia o carrera según el catálogo del rol. Las
filas conservan `rolId`, `materiaId` y `carreraId`; los nombres sólo son presentación. Se pueden
combinar roles diferentes en materias distintas, no repetir una asignación exacta y no generar
productos cartesianos entre listas.

## Editor de asignaciones docentes

Cada fila distribuye materia, cargo y dedicación en controles amplios y legibles, con horas como
campo compacto separado; el layout puede ocupar dos renglones para evitar cortar los textos.

## Buscador de materias

La materia de una asignación o de una membresía de ámbito materia se elige con un campo de solo
lectura y el botón "Buscar" ("Cambiar" si ya hay selección), igual que el Responsable en Tareas.
El modal "Buscar materia" tiene el formato de "Buscar Responsable": filtros por código y nombre de
materia y por código y nombre de carrera en una fila con "Buscar" (también Enter), conteo de
resultados, "Limpiar filtros", tabla con filas clickeables (Tab + Enter) y "Cancelar".

- Asignaciones y membresía Docente: cada fila es una materia en una carrera; elegirla completa
  ambas, por lo que no hay un selector de carrera aparte.
- Membresía Jefe de Cátedra: una fila por materia con la columna "Se dicta en"; se elige sólo la
  materia. Coordinador conserva su selector de carrera.
- Lo que no se puede elegir se ve atenuado con su motivo ("Ya asignada", "Ya tiene esta
  membresía") y la selección actual aparece resaltada.

"Nuevo docente", "Editar docente", "Nuevo usuario" y "Editar usuario" abren con el tamaño de su contenido: el ancho necesario para
leer la materia y la carrera elegidas y el alto justo hasta el último "+ Agregar…", acotados a la
ventana. Junto con "Buscar materia" (880×640 al abrir) se pueden estirar desde la esquina inferior
derecha; el cuerpo desplaza y el encabezado y el pie quedan visibles.

## Estados y accesibilidad

- En alta desde "Persona del sistema", los datos personales se muestran en una tarjeta con el
  indicador visible "Datos personales · solo lectura"; no se editan desde este flujo.
- Las fechas usan el control nativo del navegador, sin un SVG de calendario separado del campo. En
  el modal de edición de usuarios, "Fecha de nacimiento" conserva su valor ISO y su validación.

Los errores de concurrencia mantienen abierto el modal y explican que se debe recargar antes de
reintentar. Los conflictos de UPN, documento, legajo y ámbito se muestran como mensajes accionables.
Los cuatro iconos del menú administrativo son SVG inline decorativos porque cada enlace conserva su
etiqueta textual: personas agrupadas (Usuarios), birrete (Docentes), escudo (Roles) y escudo validado
(Membresía de Roles). El foco visible y el nombre accesible del enlace se mantienen en estado
expandido y colapsado.
