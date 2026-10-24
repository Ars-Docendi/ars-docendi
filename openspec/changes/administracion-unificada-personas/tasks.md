# Tasks

## 0. Buscador de materias (hecho)

### 0.1 Catálogo (backend)

- [x] 0.1.1 Test de regresión en integración: el catálogo de usuarios devuelve en `materiasPlan` el id de la materia canónica (falla hoy con el id de la fila materia–plan).
- [x] 0.1.2 Agregar `CarreraCodigo` opcional a `OpcionCatalogoDto`; completarlo en `ServicioDocentes.ObtenerCatalogosAsync` y `RepositorioUsuarios` y devolver `mp.MateriaId` en `materiasPlan` de usuarios. Tests de ambos catálogos verdes.
- [x] 0.1.3 Actualizar `docs/architecture/api-contracts-administracion.md` con el campo `carreraCodigo` de los catálogos de administración.

### 0.2 Buscador compartido (frontend)

- [x] 0.2.1 Tipos y mapeo de `carreraCodigo` en `docentesApi.ts`, `usuariosApi.ts` y `MateriaMock`.
- [x] 0.2.2 `shared/ui/BuscadorMaterias.tsx` + CSS: filtros con "Buscar"/Enter, normalización sin tildes, modos `par` y `materia`, conteo, "Limpiar filtros", no elegibles con motivo, selección resaltada, filas con `propsFilaClickeable`.
- [x] 0.2.3 `shared/ui/SelectorMateria.tsx`: campo de solo lectura + "Buscar"/"Cambiar" que abre el buscador.
- [x] 0.2.4 Clase `adoc-modal--estirable` para modales redimensionables (cuerpo con scroll, pie visible, mínimos y máximos).
- [x] 0.2.5 Tests de `BuscadorMaterias`: filtros combinados y sin tildes, aplicar con Enter, modo materia agrupado, no elegibles no seleccionables, cancelar conserva la selección.

### 0.3 Integración en pantallas

- [x] 0.3.1 `AsignacionesSelector`: reemplazar `Select` de materia y carrera por `SelectorMateria` en modo `par`; materia de otra fila como "Ya asignada". Actualizar sus tests.
- [x] 0.3.2 `MembresiasSelector`: roles de ámbito materia con `SelectorMateria` (Docente modo `par`, Jefe de Cátedra modo `materia`); cambiar el rol vacía el ámbito; duplicados como "Ya tiene esta membresía"; Coordinador sin cambios. Verificar Usuarios nuevo/editar rol.
- [x] 0.3.3 `ModalNuevoDocente` y `ModalEditarDocente` estirables con `adoc-modal--estirable`.
- [x] 0.3.4 Tamaño inicial de los modales de docente ajustado al contenido: ancho para leer materia y carrera (`size` del campo de `SelectorMateria`) y alto hasta "+ Agregar asignación", acotado a la ventana.
- [x] 0.3.5 "Nuevo usuario" y "Editar usuario" (pantalla Usuarios) estirables y ajustado al contenido con la clase compartida `adoc-modal--ajustado`, reemplazando el CSS propio de los modales de docente.

### 0.4 Documentación y verificación

- [x] 0.4.1 Actualizar `docs/product/designs/administracion-usuarios-docentes-design-spec.md` (buscador de materias y modales estirables).
- [x] 0.4.2 `dotnet test backend/ArsDocendi.slnx`, `pnpm --filter frontend test:run`, `lint`, `build`, `pnpm format:check` y `openspec validate --all --strict`.
- [x] 0.4.3 Prueba manual en la app local: alta y edición de docente con asignaciones y membresías Docente/Jefe de Cátedra, membresía Docente desde Usuarios, y redimensionado de los modales.

## 1. Permisos (identity)

- [x] 1.1 Generar con `node scripts/migrations/scaffold.mjs Identity PermisosPersonas` la migración que crea `personas.ver`, `personas.ver_ambito`, `personas.administrar` y `portal.administrar`; traduce las asignaciones de `usuarios.ver` → `personas.ver`, `docentes.ver` (sólo Jefe de Cátedra) → `personas.ver_ambito`, `usuarios.administrar` → `personas.administrar` + `portal.administrar`; suma Decanato (`personas.ver`, `personas.administrar`, `portal.administrar`) y Jefe de Cátedra (`portal.administrar`). Los permisos viejos se retiran en la tarea 6.1. `Down` deshace sólo lo agregado.
- [x] 1.2 Constantes y políticas en `Permisos.cs`; test de integración de la migración (asignaciones resultantes por rol) y `node scripts/migrations/validate.mjs`.

## 2. API de personas (Host)

- [x] 2.1 `ServicioPersonas` a partir de `ServicioDocentes`: listado, detalle, alta y edición atómicas con todos los roles de sistema, ámbito de Jefe de Cátedra, reglas de rol, cuenta y asignaciones obligatorias, y DNI único por dígitos. Tests de integración por regla.
- [x] 2.2 `PersonasController` (`/api/administracion/personas`: GET, POST, GET/PUT `{id}`, activar, desactivar, catálogos, `documento/{documento}`) con `personas.ver` / `personas.ver_ambito` / `personas.administrar`. Tests HTTP de autorización (403/404 por ámbito).

## 3. Portal administrado

- [x] 3.1 `IAdministracionPortal` en `Modules.Portal.Contracts`; refactor de `ServicioPortal` para operar sobre un perfil objetivo (propio o por `personaId`) sin cambiar Mi Portal. Los tests existentes del portal siguen verdes.
- [x] 3.2 Endpoints `/api/administracion/personas/{id}/portal/...` (lectura, descargas de CV y documento, contacto, CV, ítems, etiquetas) con `portal.administrar` y ámbito. Tests: corrección auditada con el operador, archivo vinculado a la persona, 404 fuera del ámbito.

## 4. Frontend: Personas

- [x] 4.1 Feature `personas`: rutas `/personas`, `/personas/nueva`, `/personas/:personaId`; redirecciones de `/usuarios` y `/docentes`; entrada "Personas" en `nav.ts` con `personas.ver` o `personas.ver_ambito`.
- [x] 4.2 Listado: columnas, filtros por encabezado, orden, fila clickeable, marcas "Sin rol"/"Sin cuenta", Activar/Desactivar con confirmación. Tests del filtrado y orden.
- [x] 4.3 Editor: pestañas Datos personales / Datos docentes, roles con ámbito para todos los roles, asignaciones con la materia arriba y cargo/dedicación/horas debajo, validaciones y marca de pestaña con errores, aviso de DNI repetido con debounce. Tests de validación.
- [x] 4.4 Permisos en UI: sin `personas.administrar`, datos bloqueados, sin Nueva persona ni Activar/Desactivar, "Ver" y "Volver al listado", aviso del rol.
- [x] 4.5 Perfil de Mi Portal: componentes del portal en modo sólo lectura o administración (sin cross-import entre features), descargas y acciones con `portal.administrar`. Tests de modo lectura vs. edición.

## 5. Documentación

- [x] 5.1 `api-contracts.md` y `api-contracts-administracion.md` (endpoints de personas y portal administrado, retiro de usuarios/docentes), `dependency-graph.md` (Host → Portal.Contracts para administración del portal), permisos en `data-model.md` si corresponde.
- [x] 5.2 Design spec: `administracion-usuarios-docentes-design-spec.md` pasa a describir Administrar personas.

## 6. Retiro de lo anterior y verificación

- [x] 6.1 Retirar `UsuariosController`, `DocentesController`, `ServicioDocentes`, `ServicioUsuariosAdministracion`, DTOs sin uso y `features/usuarios` / `features/docentes`; nueva migración incremental que elimina `usuarios.ver`, `usuarios.administrar` y `docentes.ver` (con `Down` que los restaura). Buscar consumidores antes de borrar.
- [ ] 6.2 Verificación proporcional: tests de las áreas tocadas (identity/administración, portal, frontend personas y portal), lint, build, `pnpm exec openspec validate --all --strict`, validación de migraciones y `--validar-recursos`.
- [ ] 6.3 Prueba manual del usuario en la app local: listado y filtros, alta y edición, DNI repetido, roles por operador (Administrativo, Jefe de Cátedra, Observador), corrección y descarga en Mi Portal.
