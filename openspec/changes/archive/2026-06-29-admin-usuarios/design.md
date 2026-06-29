## Context

La app no tiene backend de gestión de identidad todavía. Los usuarios del sistema viven en `identity.users` / `identity.user_roles` (schema Postgres), pero en desarrollo todo corre contra datos mock en memoria (ver `frontend/src/shared/auth/dev/mockUsers.ts`). Esta feature extiende ese patrón: un store mock local reemplaza al backend hasta que los endpoints reales estén disponibles.

La restricción de acceso es por rol: solo `Secretaría` y `Administración` ven la sección.

## Goals / Non-Goals

**Goals:**

- Página `/usuarios` funcional: listar, crear, desactivar y reasignar múltiples roles — todo sobre datos en memoria.
- Integración al shell existente (sidebar + router) sin romper otras features.
- Datos mock fieles al schema real (`identity.users` + `identity.user_roles`) para facilitar el reemplazo posterior por llamadas HTTP.
- Accesible solo para los roles `Secretaría` y `Administración` (guard de navegación por rol).

**Non-Goals:**

- Llamadas HTTP reales al backend.
- Persistencia entre recargas (estado se reinicia con la página).
- Gestión de permisos granulares (carrera/materia scope) — solo rol global por ahora.
- Paginación en la tabla (el mock tiene pocos registros).

## Decisions

### D1 — Store mock como módulo singleton con `useState` elevado

**Opción elegida**: El store de usuarios mock vive en `frontend/src/features/usuarios/mock/mockStore.ts` como array exportado + funciones puras de mutación. La página levanta ese array en un `useState` local al montarse.

**Alternativa descartada**: React Query con fetcher mock. Es overhead para datos que no se refrescan del servidor; un `useState` simple es suficiente y más fácil de reemplazar luego por una llamada real.

**Por qué**: Cuando llegue el backend, se reemplaza el estado local por `useQuery`/`useMutation` de React Query sin cambiar la UI.

---

### D2 — Acciones en modales, no en rutas separadas

**Opción elegida**: Crear, modificar rol y confirmar desactivación se muestran en modales sobre la tabla, no en páginas `/usuarios/nuevo`, `/usuarios/:id/editar`.

**Alternativa descartada**: Rutas separadas por acción.

**Por qué**: Son formularios cortos (2-3 campos). El modal es más ágil y no requiere rutas adicionales, manteniendo el router limpio.

---

### D3 — Guard de rol en la route, no solo en el sidebar

**Opción elegida**: La ruta `/usuarios` envuelve sus children en un componente `RequireRole` (análogo a `RequireAuth`) que redirecciona a `/` si el rol activo no está entre `["Secretaría", "Administración"]`.

**Alternativa descartada**: Ocultar solo del sidebar (invariante #7: sin rutas que aparenten funcionar pero no lo hacen).

**Por qué**: Un usuario que conozca la URL directa no debería ver la página si no tiene el rol.

---

### D4 — Datos mock derivados de `MOCK_USERS` + extensión propia

**Opción elegida**: `mockStore.ts` exporta un array inicializado copiando los 6 usuarios de `MOCK_USERS` y agrega 2-3 más para cubrir escenarios (usuario inactivo, usuario sin rol asignado). No modifica `mockUsers.ts`.

**Por qué**: `mockUsers.ts` es la fuente del mock de autenticación; mutarla podría romper el mock login. El store de usuarios es independiente.

### D5 — Filtros client-side con `useMemo`, sin estado en la URL

**Opción elegida**: Los 4 filtros (nombre, email, rol, estado) viven en `useState` local de `IndexPage` y el array filtrado se deriva con `useMemo`. No se persisten en query params.

**Alternativa descartada**: Sincronizar filtros con `URLSearchParams` para que sean compartibles/bookmarkeables.

**Por qué**: Con datos mock en memoria la búsqueda es instantánea y no hay necesidad de compartir la URL filtrada. Cuando llegue el backend, los filtros pasarán como query params a la API y el estado puede moverse a la URL en ese momento.

---

### D6 — Contexto del usuario en modales de acción

**Opción elegida**: Los modales de editar rol y crear usuario muestran nombre + UPN del usuario afectado, y tienen `marginTop: 1.5rem` entre el último campo y los botones de acción.

**Por qué**: Evita que el operador confirme acciones sobre el usuario equivocado; la separación visual reduce errores de click accidental en los botones.

### D7 — Componentes de UI desde `@ars-docendi/ui`

**Opción elegida**: Todos los controles de formulario e interfaces de tabla usan los componentes del design system: `Field` + `Input` + `Select` para formularios, `Table.*` para la tabla de usuarios, y el prop `footer` de `Modal` para los botones de acción.

**Alternativa descartada**: HTML nativo con CSS classes del design system aplicadas a mano (`adoc-field`, `adoc-input`, `adoc-table`, etc.).

**Por qué**: Los componentes de ui-lib encapsulan accesibilidad automáticamente (`aria-invalid`, `aria-describedby` y `htmlFor` autogenerados por `Field`), estilos consistentes y comportamiento de foco. Usar los componentes reduce el riesgo de drift con el design system a medida que evoluciona.

**Nota sobre el badge activo/inactivo**: `StatusBadge` de ui-lib usa `kind="aprobado"` con `label="Activo"` (badge verde con ✓) y `kind="rechazado"` con `label="Inactivo"` (badge rojo con ✗). Los iconos de aprobado/rechazado son semánticamente compatibles con activo/inactivo, por lo que se reutilizan sin agregar variantes nuevas a ui-lib.

---

### D8 — Múltiples roles por usuario con checkboxes

**Opción elegida**: El campo `rol: RolSistema` del tipo `UsuarioMock` fue reemplazado por `roles: RolSistema[]`. La selección de roles en los modales de alta y edición usa el componente `Checkbox` de ui-lib.

**Alternativa descartada**: Mantener un único rol por usuario (selector `<Select>`).

**Por qué**: El esquema real de Postgres (`identity.user_roles`) es una tabla de relación N:M, por lo que el modelo mock debe reflejar esa cardinalidad. Los checkboxes son el control más natural para selección múltiple de un conjunto pequeño y finito de opciones.

**Función de mutación**: Se renombra `cambiarRol(id, nuevoRol)` a `cambiarRoles(id, nuevosRoles[])` para reflejar el nuevo contrato.

---

### D9 — Filtros opcionales con selector de añadir/quitar

**Opción elegida**: Nombre y email son filtros fijos siempre visibles. Estado y rol son filtros opcionales: el operador los añade desde un `<select>` "Añadir filtro…" y los quita con un botón ×. Al quitar un filtro, su valor se resetea automáticamente.

**Alternativa descartada**: Mostrar los 4 filtros siempre, como antes.

**Por qué**: Reduce la densidad visual por defecto (la mayoría de las búsquedas son por nombre o email) y permite escalar a más filtros futuros sin saturar la interfaz.

**Estado local en `FiltrosUsuarios`**: El array `activados: FiltroOpcional[]` vive dentro del componente; `IndexPage` solo ve `FiltrosState` (con los valores). Cuando un filtro se quita, el componente llama `onChange` con el campo reseteado a `""`.

---

### D10 — Botones de acción en tabla como ghost

**Opción elegida**: Los botones "Editar", "Desactivar" y "Activar" de la tabla usan `variant="ghost"` de ui-lib. No se usa ningún color semántico adicional en los botones de la tabla.

**Alternativa descartada**: Botones coloreados (verde para editar, rojo para desactivar) con inline styles.

**Por qué**: En una tabla con muchas filas, botones con fondos de colores saturan visualmente. El estilo ghost comunica que son acciones secundarias disponibles en contexto, y mantiene la legibilidad de los datos. Los colores semánticos (rojo/verde) se reservan para el `StatusBadge` de la columna Estado, donde son más efectivos.

---

### D11 — Activación de usuarios con confirmación

**Opción elegida**: Los usuarios inactivos muestran un botón ghost "Activar" en lugar de "Desactivar". Al hacer clic se abre `ModalConfirmarActivacion` con botones Cancelar (secondary) y Activar (primary). La función `activarUsuario` en el mockStore pone `is_active = true`.

**Alternativa descartada**: Reactivar sin confirmación (un solo clic).

**Por qué**: Simetría con la desactivación y prevención de clicks no intencionales. El modal de activación tiene menor riesgo que el de desactivación (se puede revertir fácilmente), por lo que el botón de confirmación usa variante primary en vez de danger.

---

### D12 — Filtros opcionales en segunda fila

**Opción elegida**: El contenedor de filtros usa `flexDirection: "column"`. La primera fila contiene siempre nombre, email y el selector "Añadir filtro…". La segunda fila (condicional) se renderiza solo cuando hay filtros opcionales activos.

**Alternativa descartada**: Todo en una sola fila con flex-wrap automático.

**Por qué**: Con flex-wrap automático, al agregar dos filtros opcionales los controles se comprimían visualmente. La separación explícita en dos filas garantiza que cada fila tenga suficiente espacio horizontal sin importar el ancho de la pantalla.

---

### D13 — Modelo de persona completo en `UsuarioMock`

**Opción elegida**: `UsuarioMock` reemplaza `display_name: string` con los campos desagregados `nombre`, `apellido`, `documento` (DNI), `legajo`, `cuil`, `fecha_nacimiento`, `telefono`. Se exporta el helper `nombreCompleto(u)` para obtener "Apellido, Nombre" en tabla y modales.

**Alternativa descartada**: Mantener `display_name` y agregar los campos nuevos por separado.

**Por qué**: `display_name` era un campo derivado (nombre + apellido concatenados) que perdía información estructural. Al desagregarlo, el filtro de nombre busca en ambos campos por separado, el filtro de documento es exacto, y la tabla muestra el formato institucional "Apellido, Nombre" de forma consistente. El schema real de Postgres ya tiene las columnas separadas en `identity.users`.

---

### D14 — Ancho de selectores determinado por la opción más larga

**Opción elegida**: Los selectores de filtro usan `style={{ width: "auto" }}` en el `<select>` (sobreescribe el `width: 100%` del `.adoc-select`). El container del selector tiene `flex: "0 0 auto"` para no crecer ni encoger artificialmente.

**Alternativa descartada**: Ancho fijo hardcodeado (ej. `width: "160px"`) o `width: "100%"` expandido al container.

**Por qué**: Con `width: auto`, el browser nativo dimensiona el `<select>` para que quepa su opción más larga. Evita que opciones largas (ej. "Coordinador de Carrera") se trunquen, y que opciones cortas dejen espacio en blanco excesivo.

---

### D15 — Grilla de 2 columnas en el modal de alta

**Opción elegida**: Los campos de persona se disponen en `display: grid; grid-template-columns: 1fr 1fr; gap: 1.25rem`. Los campos de contacto y roles ocupan ancho completo (`grid-column: span 2`).

**Alternativa descartada**: Campos apilados en una sola columna.

**Por qué**: El modal tiene 520px de ancho. Con 8+ campos, el scroll vertical es excesivo en una columna. La grilla de 2 columnas reduce a la mitad el scroll manteniendo legibilidad, y agrupa campos relacionados visualmente (Nombre↔Apellido, Documento↔Legajo, CUIL↔Fecha de nacimiento).

---

### D16 — Filtros fijos cambian: Apellido, Nombre, Documento (separados)

**Opción elegida**: Los 3 filtros siempre visibles son **Apellido**, **Nombre** y **Documento** como `Input` independientes. El filtro de nombre deja de ser combinado. Los filtros opcionales pasan a ser: Legajo, Mail/UPN, Rol, Estado.

**Alternativa descartada**: Un solo campo "nombre o apellido" + email como filtros fijos.

**Por qué**: Campos separados permiten buscar "García" en apellido sin colisionar con nombres que también contengan "García". Documento es el campo de búsqueda más frecuente en contexto institucional (nómina, trámites), por lo que pasa a fijo. Legajo se vuelve opcional porque se usa menos en búsquedas cotidianas.

---

### D17 — Búsqueda insensible a tildes con `normalizarTexto`

**Opción elegida**: Se exporta `normalizarTexto(s)` desde `mockStore.ts`. Aplica `NFD` (descompone caracteres acentuados en base + combining mark) y luego elimina los combining marks (`̀-ͯ`). Se usa en `IndexPage` tanto para la query del filtro como para el campo del usuario antes de comparar.

**Alternativa descartada**: `localeCompare` con `sensitivity: "base"` — no produce un substring match limpio para `.includes()`.

**Por qué**: Al buscar "Lopez", `normalizarTexto("López")` → `"lopez"`, que incluye `"lopez"`. El operador no necesita escribir la tilde para encontrar resultados. Se aplica a apellido, nombre y documento.

---

### D18 — "Editar roles" reemplazado por "Editar" (todos los campos)

**Opción elegida**: El botón ghost de la tabla dice "Editar". El modal `ModalEditarUsuario` (en `ModalEditarRol.tsx`) replica el layout de 2 columnas de `ModalNuevoUsuario` pre-poblado con los datos actuales. Permite editar nombre, apellido, documento, legajo, CUIL, fecha de nacimiento, teléfono, UPN y roles.

**Alternativa descartada**: Mantener "Editar roles" separado y agregar un "Editar datos" adicional.

**Por qué**: Dos botones de edición aumentan la densidad de la tabla sin beneficio real. Un único "Editar" centraliza todas las modificaciones en un solo flujo, más consistente con el patrón de alta. La etiqueta corta reduce el ancho de la columna Acciones.

**Función de mutación**: `cambiarRoles` eliminada; reemplazada por `editarUsuario(lista, id, datos)` que sobreescribe todos los campos del usuario preservando `id` e `is_active`.

**Validación de UPN al editar**: Se excluye la UPN del propio usuario del array `upnsExistentes` pasado al modal, para no rechazar falsamente la UPN sin cambios.

---

### D19 — Legajo como 4 dígitos numéricos (texto)

**Opción elegida**: El campo `legajo` sigue siendo `string` (permite leading zeros), pero el mock usa 4 dígitos sin prefijo de letra (ej: `"0421"` en lugar de `"D-0421"`).

**Por qué**: El legajo real de la institución es un número de 4 dígitos. El prefijo `D-`, `S-`, `A-` era un artefacto inventado en el mock anterior que no refleja el dominio real.

---

## Risks / Trade-offs

- **Estado efímero**: Al recargar la página el store se reinicia. → Aceptado; el usuario lo sabrá por ser entorno dev. Cuando llegue el backend, se persiste en Postgres.
- **Duplicación temporal de datos**: Los usuarios del store no están sincronizados con los del mock login (son dos arrays separados). → Mitigación: cuando se integre el backend real, ambos apuntarán al mismo endpoint.
- **`RequireRole` como componente nuevo**: Agrega un poco de complejidad al router. → Es genérico y reutilizable para otras rutas restringidas que vendrán.
