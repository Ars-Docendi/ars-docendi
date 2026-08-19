## Context

La app no tiene backend de gestión de docentes todavía. Los datos de docentes eventualmente vivirán en Postgres (schema del módulo Portal o Designaciones), pero en desarrollo todo corre contra datos mock en memoria. Esta feature extiende el patrón ya establecido por `admin-usuarios`: un store mock local reemplaza al backend hasta que los endpoints reales estén disponibles.

La restricción de acceso es por rol: solo `Secretaría` y `Administración` ven la sección. El componente `RequireRole` ya existe en `shared/auth/` tras la implementación de admin-usuarios.

El modelo de docente agrega sobre el de usuario los campos específicos del dominio docente: `cargo` (categoría docente en la institución) y `materias` (lista de materias asignadas, con código y nombre). Las materias provienen de un catálogo mock que simula lo que en producción vendrá de la API Guaraní.

## Goals / Non-Goals

**Goals:**

- Página `/docentes` funcional: listar, crear, editar y activar/desactivar — todo sobre datos en memoria.
- Integración al shell existente (sidebar + router) sin romper otras features.
- Catálogo mock de materias (`MATERIAS_CATALOGO`) fiel a la estructura real (código numérico + nombre) para facilitar el reemplazo posterior por llamadas a la API Guaraní.
- Datos mock fieles al esquema real para facilitar la integración futura con backend.
- Accesible solo para los roles `Secretaría` y `Administración` (reutilizando `RequireRole`).
- Filtros client-side: Apellido, Nombre, Documento (fijos) + Código de materia, Materia, Cargo y Estado (opcionales añadibles).

**Non-Goals de la fase visual original:**

- Llamadas HTTP reales al backend.
- Persistencia entre recargas (estado se reinicia con la página).
- Paginación en la tabla (el mock tiene pocos registros).
- Integración real con la API Guaraní para el catálogo de materias.
- Gestión de horarios o carga horaria del docente (pertenece a Designaciones).

La exclusión de HTTP y persistencia fue temporal. `datos-ejemplo-y-frontend-con-api` reemplaza D2, D5, D6 y D10 en lo relativo a la fuente de registros: materias, cargos, personas, roles y designaciones se consultan por API; el patrón de filas y el modelo por asignación se mantienen como decisiones de UI.

## Decisions

### D1 — Reutilizar `RequireRole` existente

**Opción elegida**: La ruta `/docentes` envuelve sus children en el componente `RequireRole` ya existente en `shared/auth/RequireRole.tsx`, con `allowedRoles={["Secretaría", "Administración"]}`.

**Por qué**: El componente ya fue implementado y probado en admin-usuarios. Reutilizarlo mantiene el patrón consistente y no requiere nueva lógica de guard.

---

### D2 — Catálogo de materias: código como string de 5 dígitos

**Opción elegida**: `MATERIAS_CATALOGO` es un array de `{ codigo: string; nombre: string }` exportado desde `mockStore.ts`. El código es un `string` de exactamente 5 dígitos con cero a la izquierda (ej: `"03500"`, `"00310"`). El store de docentes almacena el objeto completo (`codigo + nombre`) en el campo `materias: MateriaMock[]`.

**Alternativa descartada**: `codigo: number` — pierde los ceros iniciales al mostrarse; se requeriría `padStart(5, "0")` en cada render.

**Por qué**: Los códigos de materia en Guaraní son cadenas con ceros a la izquierda (ej: `"03500"`). Usar `string` preserva la representación canónica, evita conversiones, y es fiel al tipo que vendrá de la API real. El catálogo mock usa códigos con el formato `0XXXX` donde los 4 dígitos finales identifican la materia.

---

### D3 — Selector de materias: patrón de filas añadibles

**Opción elegida**: La selección de materias en los modales de alta y edición usa un patrón de filas añadibles. Cada fila contiene un `Select` de ui-lib con el catálogo completo y un botón × para quitar la fila. Un botón "+ Agregar materia" añade una nueva fila vacía. El catálogo filtra las opciones ya seleccionadas en otras filas para evitar duplicados.

**Alternativa descartada**: Lista de `Checkbox`, uno por materia del catálogo.

**Por qué**: Con 13 materias en el catálogo, una lista de 13 checkboxes genera densidad visual excesiva en el modal, especialmente cuando la mayoría de los docentes tienen 1-3 materias asignadas. El patrón de filas añadibles muestra solo lo necesario, permite agregar N materias de forma progresiva, y escala mejor cuando el catálogo crezca a 50+ materias en producción. Se extrae como componente `MateriasSelector` reutilizado en alta y edición.

**Validación**: Se requiere al menos 1 fila con materia seleccionada. Las filas vacías (sin materia elegida) bloquean el submit con error.

---

### D4 — Filtro de Materia con selector del catálogo

**Opción elegida**: El filtro opcional "Materia" usa un `Select` de ui-lib con las opciones del `MATERIAS_CATALOGO`. El filtro "Código" es un `Input` de texto que filtra por código numérico (coincidencia substring).

**Alternativa descartada**: Un único campo de texto libre para filtrar por nombre o código de materia.

**Por qué**: Ofrecer el selector garantiza que el operador elija una materia válida del catálogo, evitando búsquedas sin resultados por errores tipográficos. El filtro de código separado cubre el caso de uso de búsqueda rápida por código cuando se lo conoce de memoria.

---

### D5 — Store mock con funciones puras (mismo patrón admin-usuarios)

**Opción elegida**: `mockStore.ts` exporta `DOCENTES_INICIALES: DocenteMock[]` + funciones puras: `agregarDocente`, `editarDocente`, `desactivarDocente`, `activarDocente`. La página levanta ese array en `useState` local.

**Por qué**: Replica exactamente el patrón de admin-usuarios. Cuando llegue el backend, se reemplaza el estado local por `useQuery`/`useMutation` de React Query sin cambiar la UI.

---

### D6 — Cargo como Select predefinido

**Opción elegida**: El campo `cargo` en `DocenteMock` es `string` restringido a los valores de `CARGOS_DOCENTES` (constante `as const`). En los modales, se renderiza como `Select` de ui-lib con las opciones predefinidas: Profesor Titular, Profesor Asociado, Profesor Adjunto, Jefe de Trabajos Prácticos, Ayudante de Primera, Ayudante de Segunda, Docente Autorizado.

**Alternativa descartada**: `Input` de texto libre.

**Por qué**: Los cargos docentes en la universidad tienen una nomenclatura institucional fija definida en el convenio colectivo y en el estatuto universitario. Un `Select` previene errores tipográficos, garantiza consistencia en los filtros y facilita la futura integración con Guaraní que devuelve estos valores como enum. El filtro de Cargo en la tabla sigue siendo de texto libre (substring) para mayor flexibilidad de búsqueda.

---

### D7 — Componentes de UI desde `@ars-docendi/ui`

**Opción elegida**: Todos los controles usan los componentes del design system: `Field` + `Input` + `Select` + `Checkbox` + `DatePicker` para formularios, `Table.*` para la tabla, `StatusBadge` para el estado, `Button` para acciones, `Modal` con prop `footer` para los botones de acción, `InlineAlert` para errores de UPN duplicada.

**Por qué**: Consistencia visual y de accesibilidad con el resto de la app. Invariante D7 de admin-usuarios aplica igual aquí.

---

### D8 — Filtros opcionales en segunda fila (mismo patrón admin-usuarios)

**Opción elegida**: Nombre, Apellido y Documento son filtros fijos siempre visibles en la primera fila. Código de materia, Materia, Cargo y Estado son opcionales añadibles. Los selectores opcionales usan `width: auto`.

**Por qué**: Reduce densidad visual por defecto y escala limpiamente. Misma UX que admin-usuarios para reducir la curva de aprendizaje del operador.

---

### D10 — Dos modos en el modal de alta: nueva persona vs. persona del sistema

**Opción elegida**: El modal de alta ofrece dos modos seleccionables con botones tab en la parte superior:

- **Nueva persona**: muestra todos los campos de datos personales (nombre, apellido, DNI, etc.) para ingresar una persona que no existe en el sistema.
- **Persona del sistema**: muestra un `Select` con `PERSONAS_SISTEMA`, la lista de personas ya registradas en el sistema de identidad. Al seleccionar una persona se muestra una tarjeta de resumen con sus datos (no editables). Los campos de cargo y materias siempre son editables en ambos modos.

**Alternativa descartada**: Un único formulario donde el campo "UPN" actúa como lookup (el operador escribe la UPN y el sistema autocompleta).

**Por qué**: El lookup por UPN es propenso a errores tipográficos y requiere lógica asíncrona. Los dos modos son más claros conceptualmente: "¿Esta persona ya está en el sistema?" → sí → elige de la lista; no → carga sus datos. Además, la lista de personas del sistema evita la creación de entradas duplicadas con variaciones ortográficas en el nombre.

**Cross-feature**: La lista `PERSONAS_SISTEMA` vive en `docentes/mock/mockStore.ts` (no cross-importa `features/usuarios`). En producción, ambas features llamarán al mismo endpoint `/api/identity/personas`. Esta duplicación de datos en el mock es temporal y documentada.

---

### D11 — Validación de materias en filas añadibles

**Opción elegida**: Se requieren dos condiciones para submit válido: (1) al menos una fila con materia seleccionada, y (2) ninguna fila vacía (todo selector debe tener una opción elegida). Si hay una fila sin selección, se bloquea el submit con error en el campo Materias.

**Por qué**: Una fila vacía indica que el operador inició una selección pero no la completó; es un error de entrada, no una opción válida de "sin materia". El mensaje de error agrupa todos los problemas de materias en el componente `MateriasSelector`.

---

### D9 — Ícono SVG inline para "docentes" en el sidebar

**Opción elegida**: Se agrega una entrada `docentes` al objeto `navIcons` en `icons.tsx` con un SVG inline de una pizarra/persona con libro (diferenciado del ícono de `usuarios`).

**Por qué**: El shell usa SVGs inline con `currentColor` para que el color cambie con el estado activo del nav item. Es el mismo patrón de todos los íconos existentes.

### D12 — Rol docente como campo separado del cargo

**Opción elegida**: `DocenteMock` tiene un campo `rol: RolDocente` (`"Docente" | "Jefe de Cátedra"`) que es el rol de sistema y determina los permisos en la plataforma (acceso a funciones de jefatura). Es totalmente independiente del cargo académico por materia. En el modal de alta/edición, el Rol se selecciona con un `Select` obligatorio.

**Alternativa descartada**: Derivar el rol del cargo más alto asignado (ej: si tiene una materia con "Profesor Titular", asumir "Jefe de Cátedra").

**Por qué**: El rol de sistema (Docente vs. Jefe de Cátedra) es una decisión administrativa explícita que no puede derivarse automáticamente del cargo académico. Un Adjunto puede ser Jefe de Cátedra si está a cargo del proyecto docente. El campo `rol` se mapea directamente al rol que tendría en la tabla `identity.user_roles` del backend real.

---

### D13 — Cargo por asignación (materia): modelo `AsignacionMateria`

**Opción elegida**: Se elimina el campo `cargo: string` global de `DocenteMock`. En su lugar, cada materia asignada lleva su propio cargo via `asignaciones: AsignacionMateria[]`, donde `AsignacionMateria = { materia: MateriaMock; cargo: CargoDocente }`. El componente `AsignacionesSelector` reemplaza a `MateriasSelector` con dos Selects por fila (materia + cargo).

**Alternativa descartada**: Mantener un cargo global para el docente y materias sin cargo individual.

**Por qué**: En la institución, un mismo docente puede ser Profesor Adjunto en una materia y Jefe de Trabajos Prácticos en otra. El modelo de cargo global es incorrecto para este dominio. El modelo `AsignacionMateria` es fiel al esquema real de Guaraní/Designaciones donde cada designación es (docente, materia, cargo).

---

### D14 — Abreviaciones de cargo en tabla

**Opción elegida**: `ABREV_CARGOS: Record<CargoDocente, string>` mapea cada cargo a su abreviación (ej: "Jefe de Trabajos Prácticos" → "JTP", "Profesor Titular" → "P. Tit.", "Ayudante de Primera" → "Ay. 1ª"). En la tabla, cada badge de asignación muestra `"CODIGO – ABREV"` (ej: `"03500 – JTP"`). En los Selects de los modales se muestra el nombre completo.

**Alternativa descartada**: Truncar el nombre con `text-overflow: ellipsis`.

**Por qué**: Truncar con ellipsis oculta información. Las abreviaciones son reconocidas por el personal administrativo universitario, son unívocas dentro del conjunto de 7 cargos, y permiten mostrar múltiples asignaciones en la celda sin saturar el layout.

---

### D15 — Horas como campo numérico por asignación

**Opción elegida**: `AsignacionMateria` agrega el campo `horas: number` (entero positivo). En `AsignacionesSelector` se renderiza como `Input type="number"` junto al Select de materia y cargo. En la tabla, el badge muestra `"CODIGO – ABREV · Xh"` (ej: `"03500 – JTP · 8h"`). La validación exige que sea un número mayor a 0.

**Alternativa descartada**: `horas` como string libre, o campo global de horas por docente (no por materia).

**Por qué**: La carga horaria en Guaraní/Designaciones es por designación (docente, materia, cargo), no global. Un docente puede tener 8h en una materia y 4h en otra. El campo numérico con validación > 0 previene valores inválidos. La representación `"Xh"` en el badge es compacta y reconocible para el personal administrativo.

---

## Risks / Trade-offs

- **Estado efímero**: Al recargar la página el store se reinicia. → Aceptado; el usuario lo sabrá por ser entorno dev. Cuando llegue el backend, se persiste en Postgres.
- **Catálogo de materias hardcodeado**: El mock no refleja el catálogo real de Guaraní. → Mitigación: la estructura de datos (`codigo + nombre`) es fiel al DTO esperado; solo cambian los datos.
- **Duplicación con admin-usuarios**: Dos stores separados para usuarios y docentes no están sincronizados. → Mitigación: cuando se integre el backend real, ambos apuntarán a los mismos endpoints. Las entidades son distintas en el dominio (identidad vs. docente).
- **Cargo sin validación semántica**: Un operador puede escribir cualquier texto como cargo. → Aceptado para la fase mock; en la integración real se validará contra el enum de Guaraní.
