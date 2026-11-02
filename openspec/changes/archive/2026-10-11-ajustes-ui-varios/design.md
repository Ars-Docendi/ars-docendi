## Context

Este change junta varios ajustes chicos de UI surgidos de una revisión visual (July). Casi todo es frontend de este repo, pero dos de los ajustes corresponden a componentes de `@ars-docendi/ui` (repo `Ars-Docendi/ui-lib`), que se consume como dependencia git (`github:Ars-Docendi/ui-lib#release/v1.0.2`).

Estado actual relevante:

- `app/shell/TopBar.tsx` muestra el buscador, las notificaciones y la ayuda, deshabilitados y con el texto "próximamente".
- Cada página arma a mano su `Breadcrumbs` y su `PageHeader` (`shared/ui/PageHeader.tsx`, con `pretitle`/`title`/`meta`/`actions`). No hay una fuente única de nombres, y los textos divergen del sidebar (`app/shell/nav.ts`).
- `features/portal/components/CampoPeriodo.tsx` tiene un `SelectorFecha` propio (select de mes + input de año) que compone un único string `"YYYY"` o `"YYYY-MM"`. `componerFecha("", mes)` devuelve `""`, así que el mes se descarta si todavía no hay año.
- El `DatePicker` de la ui-lib envuelve un `<input type="date">` y agrega su propio ícono (`.cal-ico`), pero no oculta el indicador nativo del navegador. Además, el ícono se posiciona contra el wrapper (`inline-flex`), que dentro de un `Field` en grilla se estira más que el input.
- `TablaPeriodos` usa `MenuAccionesPeriodo`, un menú kebab con solo dos acciones.

## Goals / Non-Goals

**Goals:**

- Que ningún elemento de la barra superior aparente una funcionalidad que no existe.
- Que el encabezado de cada pantalla tenga una sola fuente de "dónde estoy" (el breadcrumb) y un título igual al del sidebar.
- Que el mes de un período del Portal se pueda elegir en cualquier orden.
- Corregir el `DatePicker` para todos sus usos, no solo en Períodos.

**Non-Goals:**

- Cambiar el formato persistido de las fechas del Portal (`"YYYY"` / `"YYYY-MM"`) o de los períodos de designación.
- Rediseñar el cuerpo del detalle del pedido (tarjeta, stepper, panel de acciones). Solo cambia el encabezado.
- Quitar el número de pedido fuera del encabezado del detalle: Mis pedidos, los modales y el subtítulo del formulario lo conservan.
- Prototipar en Pencil (ver D6).

## Decisions

### D1. Los componentes reutilizables se corrigen en la ui-lib, no con overrides locales

El fix del `DatePicker` y el nuevo selector de mes/año van en `Ars-Docendi/ui-lib` y se publican como `v1.0.3`.

- **Por qué**: el componente vive ahí, y cualquier otra pantalla que use `DatePicker` (Certificaciones, Nuevo usuario) tiene el mismo problema.
- **Alternativa descartada**: un override CSS en `frontend/`. Es más rápido, pero parchea el design system desde afuera y generaría deuda técnica.
- **Flujo de release de la ui-lib**: PR a `main` → tag `v1.0.3` → el workflow `release.yml` publica `release/v1.0.3` con el `dist` → en este repo se bumpea `frontend/package.json`. En el mismo PR de la ui-lib se alinea el campo `version` de su `package.json`, que hoy dice `1.0.1` aunque el último tag es `v1.0.2`.

### D2. Fix del `DatePicker`: input a ancho completo e indicador nativo invisible pero clickeable

Dentro de `.adoc-date`, el input pasa a `width: 100%`. El `::-webkit-calendar-picker-indicator` queda transparente y posicionado sobre `.cal-ico`, así se ve un único ícono (el de la librería) y hacer click sobre él sigue abriendo el calendario nativo.

- **Alternativa descartada**: ocultar `.cal-ico` y dejar el ícono nativo, porque se pierde la consistencia visual con el resto de los íconos de la librería.

### D3. `MonthYearPicker` en la ui-lib con el diseño actual del Portal

Se extrae `SelectorFecha` de `CampoPeriodo` a un componente `MonthYearPicker` de la ui-lib:

- Select de mes opcional ("ene…dic") + input de año de 4 dígitos, con el mismo layout que hoy (`.portal-fecha`).
- `value` / `onChange` con strings `"YYYY"` o `"YYYY-MM"`, sin cambios de formato.
- Guarda el mes elegido en un estado interno mientras no haya año, y emite el valor compuesto cuando el año se completa.
- Props siguiendo el estilo de la librería: `value`, `onChange`, `disabled`, `invalid`, `aria-label`. Los nombres van en inglés, como el resto de la ui-lib (el invariante #13 aplica a este repo).
- Se entrega con su `.stories.tsx`.

`CampoPeriodo` sigue existiendo en el Portal (rango desde/hasta + checkbox "en curso"), pero usa `MonthYearPicker`. Si el usuario deja un mes elegido sin año, el campo muestra el error inline "Completá el año", con el mismo patrón que el error de "Desde".

- **Alternativas descartadas**: permitir un valor parcial `"-MM"` (ensucia el formato que usan `ordenarPorPeriodo` y el backend); reemplazar por `DatePicker` (obliga a elegir un día y cambia el formato guardado).

### D4. Convención de encabezados

| Capa       | Regla                                                                                                                                                                                                                                           |
| ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Breadcrumb | `Inicio › Página` (o `Inicio › Pantalla de origen › Página`). Nunca incluye el grupo del sidebar: los grupos no son pantallas y quedaban como niveles no clickeables. Se revisó tras probar la UI; la versión anterior agregaba "Designaciones" |
| Pretitle   | No se usa. Se quita la prop de `PageHeader` para que no se vuelva a usar                                                                                                                                                                        |
| Título     | Igual al label del sidebar, con mayúscula solo en la primera palabra. Excepciones: "Administración de usuarios / docentes / roles" y "Mis docentes" (Jefe de Cátedra)                                                                           |
| Meta       | Solo datos que no aparecen en otro lugar de la pantalla (conteos, período). Nunca el rol, que ya se ve en la barra superior                                                                                                                     |

El grupo `"DESIGNACIONES"` de `nav.ts` pasa a `"Designaciones"` para quedar igual que los otros grupos. Visualmente no cambia nada, porque `.adoc-nav-section` aplica `text-transform: uppercase`.

Nuevo/Editar pedido dejan su `h1` propio y pasan a usar `PageHeader`. El subtítulo actual del formulario pasa a ser el `meta`.

### D5. Acciones de Períodos como botones directos

Se reemplaza `MenuAccionesPeriodo` por botones en la celda, siguiendo el patrón de `TablaMisPedidos`: botón ghost "Editar" + botón con ícono para eliminar, con su `aria-label`. `MenuAccionesPeriodo` se elimina si deja de tener usos.

### D6. Excepción: sin prototipo Pencil

La guía del proyecto pide prototipar en Pencil antes del front. En este change no se hace, por decisión explícita: son ajustes sobre diseños existentes, y `MonthYearPicker` replica tal cual el control actual del Portal. Los design-specs de las pantallas afectadas sí se actualizan (invariante #12).

### D7. El breadcrumb del detalle refleja el origen

Hoy el detalle (`/designaciones/pedidos/:id`) tiene "Revisión" fijo en el breadcrumb y en el link de error. Por eso, un Jefe de Cátedra que entra desde "Mis pedidos" ve un breadcrumb que no corresponde y que lo lleva a una ruta para la que no tiene permiso. "Volver" no tiene este problema porque usa el historial (`navegar(-1)`).

Solución:

- Mis pedidos y Revisión navegan al detalle pasando el origen en el estado de navegación: `navegar(url, { state: { origen: "mis-pedidos" | "revision" } })`.
- El detalle resuelve el nivel intermedio del breadcrumb (label + ruta) a partir de `location.state.origen`.
- Si no hay estado (link directo o pestaña nueva), se deduce por permiso: con `designaciones.revisar` va a Revisión, sin ese permiso a Mis pedidos.
- El link de error del detalle usa la misma resolución.

**Alternativas descartadas:**

- Query param `?desde=…`: ensucia la URL, y un link compartido arrastra el origen de otra persona.
- Resolver solo por permiso: falla para quien tiene acceso a las dos pantallas.

### D8. Un único modal de confirmación de borrado

Hoy hay tres copias del mismo diálogo, que ya divergieron: `ModalEliminarPeriodo` (sin estado de carga), `ModalEliminarPedido` (con carga) y el `ModalConfirmarEliminar` del Portal (sin carga ni error). Se reemplazan por `shared/ui/ModalConfirmarEliminar`:

- Props: `open`, `onOpenChange`, `titulo`, `objeto` (un `ReactNode` con lo que se borra, por ejemplo `el período <strong>"X"</strong>`), `error?`, `eliminando?`, `onConfirmar`.
- Texto fijo: "¿Estás seguro de que querés eliminar {objeto}?" + "Esta acción no se puede deshacer."
- Mientras `eliminando` es verdadero, el botón Eliminar muestra la carga y Cancelar queda deshabilitado, así que no se puede confirmar dos veces.
- El error se muestra con un InlineAlert titulado "No se pudo eliminar".

Vive en `shared/ui` porque lo usan dos features (designaciones y portal). No va a la ui-lib porque arma textos de dominio en español.

- **Alternativa descartada**: un `ConfirmDialog` genérico en la ui-lib. Hoy solo existe el caso de borrado; se puede generalizar cuando aparezca otro.

## Risks / Trade-offs

- [El release de la ui-lib bloquea las tareas que dependen de él] → El trabajo en este repo que no depende de la ui-lib (TopBar, encabezados, acciones de Períodos) se puede hacer en paralelo. El bump va al final.
- [El indicador nativo del date picker se comporta distinto en Firefox y Safari] → Verificar en Chrome, Firefox y Safari. En Firefox el indicador no es un pseudo-elemento estilable, así que hay que confirmar que no aparezca duplicado.
- [Los tests que buscan textos exactos (títulos, "DESIGNACIONES", menú kebab) se rompen] → Actualizarlos dentro del mismo change.
- [Mantener "Administración de …" rompe la regla título = sidebar en tres pantallas] → Es una excepción aceptada explícitamente y queda documentada en la spec `encabezado-paginas`.

## Migration Plan

1. PR en `Ars-Docendi/ui-lib` (fix `DatePicker` + `MonthYearPicker` + versión) → merge → tag `v1.0.3` → verificar que exista `release/v1.0.3`.
2. En esta rama: bump de `@ars-docendi/ui` a `#release/v1.0.3` y `pnpm install`.
3. Rollback: revertir el PR y volver a `#release/v1.0.2`. No hay datos ni migraciones involucradas.

## Open Questions

Ninguna. El origen del breadcrumb del detalle se resolvió en D7.
