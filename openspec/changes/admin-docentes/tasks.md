## 0. Modelo base

- [ ] 0.1 `CurrentUser` (shared/auth/useCurrentUser.ts) — agregar campo `upn: string`; poblar en `STUB_USER` y en cada `currentUser` de `MOCK_USERS`
- [ ] 0.2 Agregar Gustavo Ruiz (UPN `gustavo.ruiz@unlam.edu.ar`) a `DOCENTES_INICIALES` con `roles: ["Docente", "Jefe de Cátedra"]` — único ejemplo de doble rol. Su UPN coincide con el mock user JdC para que "Mis Docentes" funcione

## 1. Shell — ícono y sidebar

- [x] 1.1 Agregar `docentes` al objeto `navIcons` en `frontend/src/app/shell/icons.tsx` (SVG inline de persona con libro/pizarra)
- [x] 1.2 Agregar ítem `Docentes` al grupo "Configuración" de Secretaría y Administración en `frontend/src/app/shell/nav.ts`; agregar `Mis Docentes` al grupo "Trabajo" de Jefe de Cátedra

## 2. Mock store de docentes

- [x] 2.1 `MateriaMock` (`codigo: string` 5 dígitos) y `MATERIAS_CATALOGO`
- [x] 2.2 `normalizarTexto(s)` y helper `nombreCompleto(d)`
- [x] 2.3 Funciones puras: `agregarDocente`, `editarDocente`, `desactivarDocente`, `activarDocente`
- [x] 2.4 `CARGOS_DOCENTES` (7 cargos, `as const`) y tipo `CargoDocente`
- [x] 2.5 `PERSONAS_SISTEMA` (7 personas del sistema de identidad)
- [ ] 2.6 Agregar `RolDocente = "Docente" | "Jefe de Cátedra"` y `ROLES_DOCENTE`. `DocenteMock.roles: RolDocente[]` (array, permite múltiples). Alta crea `roles: [rolSeleccionado]`; edición permite checkboxes con ambos
- [ ] 2.7 Agregar `AsignacionMateria = { materia: MateriaMock; cargo: CargoDocente; horas: number }` — reemplaza `cargo: string` + `materias: MateriaMock[]` en `DocenteMock`
- [ ] 2.8 Agregar `ABREV_CARGOS: Record<CargoDocente, string>` con abreviaciones: Titular, Adjunto, Asociado, JTP, Ay. 1ra, Ay. 2da. Sin cargo "Docente Autorizado"
- [ ] 2.9 Actualizar `DocenteMock` → agregar `rol: RolDocente`, reemplazar `cargo`+`materias` por `asignaciones: AsignacionMateria[]`
- [ ] 2.10 Actualizar `DOCENTES_INICIALES` con el nuevo modelo (rol + asignaciones con cargos individuales)

## 3. Feature routing

- [x] 3.1 Crear `frontend/src/features/docentes/routes.tsx` envuelta en `RequireRole` con `allowedRoles={["Secretaría", "Administración", "Jefe de Cátedra"]}`
- [x] 3.2 Registrar `docentesRoutes` en `frontend/src/app/router.tsx`

## 4. Página principal — listado

- [x] 4.1 Crear `frontend/src/features/docentes/pages/IndexPage.tsx` con `useState` del store, `useMemo` para filtros y orquestación de modales. Si el usuario es JdC: título "Mis Docentes", filtro automático por materias propias (match UPN → docente → asignaciones), ocultar botón "Nuevo docente"
- [x] 4.2 Crear `frontend/src/features/docentes/components/TablaDocentes.tsx` — columnas: Apellido y Nombre | Documento | Legajo | Cargo | Materias (badges) | Estado (StatusBadge) | Acciones (botones ghost). Scroll horizontal automático

## 5. Filtros

- [x] 5.1 Crear `frontend/src/features/docentes/components/FiltrosDocentes.tsx` — primera fila fija: Apellido, Nombre, Documento + selector "Añadir filtro…". Segunda fila condicional: Código de materia (Input), Materia (Select con catálogo), Cargo (Input), Estado (Select activo/inactivo). Selectores con `width: auto`
- [x] 5.2 Integrar `FiltrosDocentes` en `IndexPage.tsx` con filtrado client-side via `useMemo` y `normalizarTexto`

## 6. Componente AsignacionesSelector

- [ ] 6.1 Crear `frontend/src/features/docentes/components/AsignacionesSelector.tsx` — recibe `rows: AsignacionRow[]` (`{ materia: string; cargo: string; horas: string }[]`) y `onChange`. Cada fila: `Select` de materia + `Select` de cargo + `Input` numérico de horas + botón ×. Botón "+ Agregar asignación". Filtra materias ya usadas en otras filas. Validación: horas > 0. Muestra `error`. Botón × oculto si solo hay 1 fila

## 7. Modal — crear docente (actualizado)

- [ ] 7.1 Reescribir `ModalNuevoDocente.tsx` — toggle modo (Nueva persona / Persona del sistema). Agregar `Select` de Rol (`ROLES_DOCENTE`, obligatorio). Reemplazar Cargo global + MateriasSelector por `AsignacionesSelector`

## 8. Modales — cambio de estado

- [x] 8.1 `ModalConfirmarDesactivacion.tsx` — creado
- [x] 8.2 `ModalConfirmarActivacion.tsx` — creado

## 9. Modal — editar docente (actualizado)

- [ ] 9.1 Actualizar `ModalEditarDocente.tsx` — dividir en dos pestañas con `Tabs` (ui-lib):
  - Pestaña "Datos docentes" (default): checkboxes de Roles + `AsignacionesSelector`
  - Pestaña "Datos personales": campos personales editables (Nombre, Apellido, Doc, Legajo, CUIL, Fecha, UPN, Teléfono)
  - Un único footer "Guardar cambios" valida todo; `InlineAlert` warning en pestaña docentes si hay errores en personales
  - Al abrir un nuevo docente, siempre volver a pestaña "Datos docentes"

## 10. Tabla y filtros (actualizados)

- [ ] 10.1 Actualizar `TablaDocentes.tsx` — agregar columna "Rol"; cambiar columna Materias → "Asignaciones" con badges `"CODIGO – ABREV_CARGO"` (sin horas) usando `ABREV_CARGOS`; columnas Estado y Acciones con `whiteSpace: nowrap` + `width: 1%` para que se compriman y le cedan espacio a Asignaciones
- [ ] 10.2 Actualizar `FiltrosDocentes.tsx` — agregar filtro opcional "Rol" (Select: Docente / Jefe de Cátedra); actualizar `FiltrosState` con campo `rol: string`
- [ ] 10.3 Actualizar `IndexPage.tsx` — agregar `rol: ""` a `FILTROS_VACIOS`; actualizar lógica de filtrado para `asignaciones` (cargo substring sobre `a.cargo`) y `rol` (exacto)

## 11. Componentes de ui-lib utilizados

- [x] 11.1 `Table.*`, `StatusBadge`, `Button`, `Modal`, `Field`, `Input`, `Select`, `DatePicker`, `InlineAlert`, `Breadcrumbs`, `PageHeader`

## 11. Verificación

- [x] 11.1 Secretaría ve "Docentes" en sidebar y la página carga correctamente
- [x] 11.2 Otro rol (ej. Docente, Decanato) es redirigido a `/` al intentar acceder a `/docentes`
- [ ] 11.3 Alta modo "Nueva persona": campos completos + al menos una asignación → docente en tabla con badges "CODIGO – ABREV"
- [ ] 11.4 Alta modo "Persona del sistema": seleccionar persona + al menos una asignación → docente en tabla con datos de la persona
- [ ] 11.5 Alta: UPN duplicada (modo nueva y existente) muestra InlineAlert
- [ ] 11.6 Alta: fila de materia vacía bloquea el submit con error
- [ ] 11.7 Alta: cargo no seleccionado muestra "Campo obligatorio"
- [ ] 11.8 MateriasSelector: "+ Agregar materia" añade fila; × quita fila; opciones ya elegidas no aparecen en otras filas
- [x] 11.9 Desactivar: modal → badge rojo "Inactivo", botón cambia a "Activar"
- [x] 11.10 Activar: modal → badge verde "Activo", botón cambia a "Desactivar"
- [ ] 11.11 Editar: cargo y materias pre-cargados correctamente; cambios guardados en tabla
- [x] 11.12 Filtros fijos (Apellido, Nombre, Documento): filtran e ignoran tildes
- [x] 11.13 Filtros opcionales: se añaden/quitan; Materia selector usa catálogo completo
