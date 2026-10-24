# Design spec — Administrar personas

## Alcance

La pantalla `/personas` reemplaza a Usuarios y Docentes: muestra la identidad canónica con su
cuenta, sus roles con ámbito, sus asignaciones y su perfil de Mi Portal, sin duplicar cuentas ni
persistir un indicador `es_docente`. `/usuarios` y `/docentes` (con `?personaId=`) redirigen.

## Listado

- Columnas: Nombre (apellido, nombre), Documento, Legajo, Rol, Asignaciones, Estado y Acciones.
- Rol: un badge por rol sin repetir (Jefe de Cátedra en verde, el resto neutro); "Sin rol" marcado
  para corregir. Asignaciones: badge "código – cargo abreviado" con la materia y las horas al
  pasar el mouse. Estado: Activo, Inactivo o Sin cuenta.
- Filtros por encabezado (`FiltroEncabezado`): texto en Nombre, Documento, Legajo y Asignaciones;
  opciones en Rol y Estado. Orden por encabezado en Nombre, Documento, Legajo y Estado
  (ascendente, descendente, sin orden).
- La fila completa abre la persona (mouse o Tab + Enter). Acciones: "Editar" (o "Ver" sin permiso
  de administración) y "Activar"/"Desactivar" con confirmación.

## Alta y edición

Pantalla completa (`/personas/nueva`, `/personas/{id}`) con pestañas "Datos personales" (abre
primero) y "Datos docentes", y pie fijo con Cancelar y Guardar.

- Datos personales: Nombre, Apellido, Documento, Legajo, CUIL, Fecha de nacimiento, UPN y Teléfono.
  El DNI repetido se avisa mientras se escribe, nombrando a la persona que lo tiene.
- Roles y ámbitos: cada fila con su rol y lo que pide (Docente: materia y carrera; Jefe de Cátedra:
  materia; Coordinador: carrera; roles de departamento: nada). Siempre queda al menos una fila.
- Asignaciones: la materia y la carrera completas en un renglón y debajo Cargo, Dedicación y Horas.
  Obligatorias con rol Docente o Jefe de Cátedra.
- Perfil en Mi Portal: las mismas secciones y componentes que la pantalla Mi Portal, a todo el
  ancho, con descarga de CV y documentos. Editable con `portal.administrar`; si no, sin acciones.
- Si al guardar hay errores en la pestaña oculta, se marca con un punto y se avisa.
- Sin `personas.administrar`: datos bloqueados, sin Nueva persona ni Activar/Desactivar, un aviso
  con lo que puede hacer el rol y "Volver al listado" en lugar de Guardar.

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
- "Buscar materia" abre en 880×640 y se puede estirar desde la esquina inferior derecha; el cuerpo
  desplaza y el encabezado y el pie quedan visibles.

## Estados y accesibilidad

- Las fechas usan el control nativo del navegador.
- Los errores de concurrencia explican que se debe recargar antes de reintentar. Los conflictos de
  UPN, documento, legajo y ámbito se muestran como mensajes accionables.
- El menú de Configuración muestra "Personas" (icono de personas agrupadas) y "Roles"; cada enlace
  conserva su etiqueta textual y su foco visible en estado expandido y colapsado.
