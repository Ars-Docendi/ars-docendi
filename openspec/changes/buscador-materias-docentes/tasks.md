# Tasks

## 1. Catálogo (backend)

- [x] 1.1 Test de regresión en integración: el catálogo de usuarios devuelve en `materiasPlan` el id de la materia canónica (falla hoy con el id de la fila materia–plan).
- [x] 1.2 Agregar `CarreraCodigo` opcional a `OpcionCatalogoDto`; completarlo en `ServicioDocentes.ObtenerCatalogosAsync` y `RepositorioUsuarios` y devolver `mp.MateriaId` en `materiasPlan` de usuarios. Tests de ambos catálogos verdes.
- [x] 1.3 Actualizar `docs/architecture/api-contracts-administracion.md` con el campo `carreraCodigo` de los catálogos de administración.

## 2. Buscador compartido (frontend)

- [x] 2.1 Tipos y mapeo de `carreraCodigo` en `docentesApi.ts`, `usuariosApi.ts` y `MateriaMock`.
- [x] 2.2 `shared/ui/BuscadorMaterias.tsx` + CSS: filtros con "Buscar"/Enter, normalización sin tildes, modos `par` y `materia`, conteo, "Limpiar filtros", no elegibles con motivo, selección resaltada, filas con `propsFilaClickeable`.
- [x] 2.3 `shared/ui/SelectorMateria.tsx`: campo de solo lectura + "Buscar"/"Cambiar" que abre el buscador.
- [x] 2.4 Clase `adoc-modal--estirable` para modales redimensionables (cuerpo con scroll, pie visible, mínimos y máximos).
- [x] 2.5 Tests de `BuscadorMaterias`: filtros combinados y sin tildes, aplicar con Enter, modo materia agrupado, no elegibles no seleccionables, cancelar conserva la selección.

## 3. Integración en pantallas

- [x] 3.1 `AsignacionesSelector`: reemplazar `Select` de materia y carrera por `SelectorMateria` en modo `par`; materia de otra fila como "Ya asignada". Actualizar sus tests.
- [x] 3.2 `MembresiasSelector`: roles de ámbito materia con `SelectorMateria` (Docente modo `par`, Jefe de Cátedra modo `materia`); cambiar el rol vacía el ámbito; duplicados como "Ya tiene esta membresía"; Coordinador sin cambios. Verificar Usuarios nuevo/editar rol.
- [x] 3.3 `ModalNuevoDocente` y `ModalEditarDocente` estirables con `adoc-modal--estirable`.
- [x] 3.4 Tamaño inicial de los modales de docente ajustado al contenido: ancho para leer materia y carrera (`size` del campo de `SelectorMateria`) y alto hasta "+ Agregar asignación", acotado a la ventana.

## 4. Documentación y verificación

- [x] 4.1 Actualizar `docs/product/designs/administracion-usuarios-docentes-design-spec.md` (buscador de materias y modales estirables).
- [x] 4.2 `dotnet test backend/ArsDocendi.slnx`, `pnpm --filter frontend test:run`, `lint`, `build`, `pnpm format:check` y `openspec validate --all --strict`.
- [ ] 4.3 Prueba manual en la app local: alta y edición de docente con asignaciones y membresías Docente/Jefe de Cátedra, membresía Docente desde Usuarios, y redimensionado de los modales.
