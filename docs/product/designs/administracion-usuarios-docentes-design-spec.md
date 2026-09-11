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
