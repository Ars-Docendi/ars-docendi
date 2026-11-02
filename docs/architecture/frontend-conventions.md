# Convenciones de navegación del frontend

Reglas transversales de cómo se llega a una pantalla, cómo se sale y qué conserva. Viven en
`frontend/src/shared/navegacion/` y `frontend/src/shared/ui/`; las features las usan sin importarse entre sí.
Origen: change `navegacion-consistente`.

## Router

La app usa un router de datos (`createBrowserRouter`). Todo lo que bloquea o intercepta la navegación
(`useBlocker`) cuelga de un único componente, `GestorDeNavegacion`, montado en el shell: react-router sólo
admite un bloqueador activo. Los tests de componentes con `MemoryRouter` no lo necesitan; los de
navegación usan `createMemoryRouter`.

## "Volver"

- Abrir un detalle pasa `state.volverA = pathname + search` de la lista (`useAbrirDetalle`, `EnlaceFila`).
- `resolverVolver(tipo, candidata, permisos)` (`shared/navegacion/volver.ts`) acepta la candidata sólo si es una
  ruta relativa (sin esquema, `//`, `\` ni hash) que está en la lista permitida de la entidad y que el usuario
  puede abrir; si no, usa el destino fijo. Para sumar una entidad se agrega su entrada a `CONFIGURACION`.
- El botón es `EnlaceVolver`: un `Link` con el aspecto del botón secundario, rotulado "Volver a <destino>". La miga
  de la lista usa el mismo destino (`migaDeVolver`) y se oculta debajo de 560 px (`Migas conVolver`).
- Un detalle que enlaza a otro (tarea hija, edición) pasa su destino ya resuelto con `propagarVolver`, nunca su
  propia URL. Guardar una edición navega con `replace`.
- Nunca se usa `navigate(-1)`. Los `<a href>` internos están prohibidos: recargan la app; usar `Link`.

## Estado de pantalla en la URL

`useFiltrosURL(valoresPorDefecto, prefijo?)`, `useOrdenURL(columnas, prefijo?)` y `usePestanaURL(validas, defecto, opciones)`
(`shared/navegacion/estadoURL.ts`). Los valores por defecto no se escriben. Filtros y orden reemplazan la entrada del
historial; las pestañas la agregan, salvo las del editor de Personas (`reemplazar`), que son dos vistas de un mismo
formulario. Dos tablas en una página usan prefijos (`mias.`, `todas.`; en el inicio de Tareas, `g.` para "Generales" y
`p<8 primeros caracteres del id>.` para cada proyecto, ver `prefijoDeCuadro`). `valoresPorDefecto` debe ser una constante o
estar memorizado. `useFiltrosURL` devuelve un eco local de lo último escrito hasta que la URL lo alcanza: la navegación es
asíncrona y, sin el eco, las teclas rápidas se pisan y el filtro de texto pierde letras.

## Scroll

`RestauradorDeScroll` guarda el scroll de `.adoc-main` por entrada del historial y por `pathname + search`, lo restaura
en Atrás/Adelante y en "Volver" cuando el contenido ya es lo bastante alto, lo reinicia en una página nueva y lo
conserva en un `replace` que sólo cambia el query.

## Cambios sin guardar

`useGuardiaDeCambios(sucio)` marca la página; devuelve `permitirSalida()`, que se llama justo antes de navegar tras un
guardado exitoso. `GestorDeNavegacion` pregunta "Tenés cambios sin guardar." ("Seguir editando" con el foco /
"Descartar cambios") al salir de la página, y también avisa en `beforeunload`. Un cambio de query de la misma página no
bloquea. Cambiar de rol (`ModalCambiarRol`) pregunta lo mismo **antes** de aplicarlo; al descartar llama a
`liberarGuardias()` para que no se pregunte de nuevo al navegar. Abrir una notificación con la página sucia deja el panel
abierto bajo el diálogo: el panel se cierra cuando la ruta cambia de verdad.

## Capas (modales y paneles)

`shared/ui/Modal` tiene la firma del `Modal` de la librería y usa la carcasa `ModalDeFormulario`: trampa de foco, foco
inicial y devuelto, Esc sólo en la capa superior (`capas.ts`), confirmación de cambios con `sucio`, sin cierre con
`pendiente`. `shared/ui/Drawer` hace lo mismo para paneles. El botón Atrás del navegador cierra la capa superior y no
sale de la página. No usar `Modal` ni `Drawer` de `@ars-docendi/ui` directamente.

Tamaños: `confirmacion` (480 px), `formulario` (600 px) y `libre` (el ancho lo pone la clase del consumidor).
Distribución: `adoc-form`, `adoc-form-seccion` y `adoc-form-fila cols-N` (`shared/ui/formulario.css`).

## Fechas y horas

`CampoFechaISO` (valor `aaaa-mm-dd`) o `CampoFecha`/`CampoHora` (texto enmascarado) en lugar de `type="date"`, `type="time"`
o `DatePicker`.

## Página: título, foco, anuncio

`PageHeader` fija `document.title = "<página> · Ars Docendi"` desde su `h1`. En cada cambio de ruta que no sea sólo de
query, `SeguimientoDePagina` enfoca el `h1` (`tabIndex=-1`, sin contorno), lo anuncia en una región `aria-live` educada y
`EnlaceSaltar` ofrece "Saltar al contenido". Una pantalla nueva debe usar `PageHeader`.

## Rutas

`RequirePermission` muestra "No tenés acceso a esta página" en la misma URL (no redirige). Una ruta desconocida muestra
"Página no encontrada" y un error de render queda dentro del shell (`errorElement`). Sin sesión se redirige al login, que
vuelve a la ruta pedida con su query y hash si es una ruta relativa de la app (el backend valida `returnUrl` igual).

## Pestañas y filas

`Pestanas` / `PanelPestana` (`shared/ui/Pestanas.tsx`): tabindex itinerante, flechas, Inicio y Fin, `tabpanel` etiquetado.
Una fila clickeable usa `propsFilaClickeable` y un `EnlaceFila` real en la celda principal.
