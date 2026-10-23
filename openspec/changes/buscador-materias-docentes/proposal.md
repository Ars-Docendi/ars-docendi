# Proposal

## Why

En "Nuevo docente" y "Editar docente" la materia de cada membresía y de cada asignación se elige en un `Select` con todo el catálogo (cientos de materias, varias repetidas por carrera). Encontrar una materia por código o acotarla a una carrera es lento y propenso a errores. Tareas ya resolvió el mismo problema para personas con un buscador emergente (`PopupBuscarResponsable`); se aplica ese patrón a las materias. Además, los modales de docente quedan apretados en el ancho fijo de 520px de la ui-lib.

## What Changes

- Nuevo buscador emergente "Buscar materia" con el formato de `PopupBuscarResponsable`: filtros por código de materia, nombre de materia, código de carrera y nombre de carrera, botón "Buscar", tabla de resultados con filas clickeables y "Cancelar".
- En asignaciones, el `Select` de materia y el de carrera se reemplazan por un campo de solo lectura más "Buscar"/"Cambiar": cada resultado es una materia en una carrera y elegirlo completa ambas.
- En membresías (`MembresiasSelector`, compartido con Usuarios), los roles con ámbito materia usan el mismo buscador. Docente elige materia + carrera; Jefe de Cátedra elige la materia canónica (una fila por materia con sus carreras). Coordinador conserva su `Select` de carrera.
- Las materias que no se pueden elegir se muestran deshabilitadas con el motivo ("Ya asignada", "Ya tiene esta membresía") en lugar de desaparecer.
- Los modales "Nuevo docente" y "Editar docente" abren con el tamaño de su contenido (ancho para leer materia y carrera completas, alto hasta "+ Agregar asignación"), acotado a la ventana. Junto con "Buscar materia", se pueden estirar desde la esquina inferior derecha; el cuerpo desplaza y encabezado/pie quedan visibles.
- El catálogo de materias–carrera de administración incluye el código de carrera (`carreraCodigo`), necesario para filtrar por él.
- Corrección: el catálogo de Usuarios devuelve en `materiasPlan` el id de la fila materia–plan en lugar del id de la materia, por lo que la membresía Docente no encuentra carreras para ofrecer. Se corrige con test de regresión.

## Capabilities

### New Capabilities

- `buscador-materias`: buscador emergente de materias por código y carrera para las pantallas administrativas.

### Modified Capabilities

- `crear-docente`: las asignaciones eligen materia y carrera con el buscador; las materias ya usadas se ven deshabilitadas en vez de ocultarse.

## Impact

- **Frontend:** `frontend/src/shared/ui/` (buscador nuevo, `MembresiasSelector`), `frontend/src/features/docentes/` (`AsignacionesSelector`, `ModalNuevoDocente`, `ModalEditarDocente`, modelos y mapeo de catálogo), `frontend/src/features/usuarios/api/usuariosApi.ts` (tipo del catálogo).
- **Backend (Host + Shared, sin módulos):** `OpcionCatalogoDto` agrega `CarreraCodigo` opcional; `ServicioDocentes.ObtenerCatalogosAsync` y `RepositorioUsuarios` lo completan; `RepositorioUsuarios` devuelve `MateriaId` en `materiasPlan`. Cambio aditivo en `GET /api/administracion/docentes/catalogos` y `GET /api/administracion/usuarios/catalogos`; no hay consumidores cross-module.
- **Sin cambios** de schema, migraciones, reglas de negocio, permisos ni grafo de dependencias. Las validaciones de ámbito y unicidad siguen en backend.
- **Docs:** `docs/architecture/api-contracts.md` (campo nuevo) y `docs/product/designs/administracion-usuarios-docentes-design-spec.md`.
- **Rollback:** revertir el diff; el campo nuevo es opcional y la corrección del id no requiere datos.
