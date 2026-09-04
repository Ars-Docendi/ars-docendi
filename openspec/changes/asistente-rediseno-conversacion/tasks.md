Cada fase es un commit. Antes de cada commit: `pnpm exec prettier --write <archivos tocados>`, `pnpm --filter frontend lint`, `pnpm --filter frontend exec tsc -b`, `pnpm --filter frontend test:run`. Mensaje conventional en español con el tono del repo; en las fases de bugs, cierre «Verificado en rojo: …» y siempre «N tests en verde.». Los tests nuevos van en archivos hermanos de `asistente.test.tsx` (mismo montaje con `QueryClientProvider`, mismos `vi.spyOn` sobre `api.obtenerCapacidades` / `api.consultar`, `userEvent` real) para respetar el cap de ~300 líneas. Leer `.claude/skills/react-features-guide/SKILL.md` antes de tocar `frontend/src`.

## 1. fix(asistente): Enter en vuelo + hilo perdido en 404

- [x] 1.1 Test rojo primero — `asistente.turnos.test.tsx` (nuevo): «Enter en vuelo no manda un segundo turno»: `consultar` con promesa pendiente; `type("a{Enter}")`, `type("b{Enter}")`; `expect(consultar).toHaveBeenCalledTimes(1)`; resolver; el segundo texto sigue en el campo. Hoy falla: se llama dos veces con claves distintas.
- [x] 1.2 Test rojo primero — «Un 404 reinicia el hilo»: primer turno OK (hilo A); segundo rechaza con `Object.assign(new Error(), { isAxiosError: true, response: { status: 404 } })`; tercero → `mock.calls[2][0].hilo === null`. Hoy falla: repite el hilo A.
- [x] 1.3 `hooks/useAsistente.ts`: `preguntar` ignora el envío si ya hay un turno en vuelo (guard en el hook). `components/PanelAsistente.tsx`: el `onKeyDown` no llama a `enviar` mientras `enVuelo`.
- [x] 1.4 `errores.ts`: exportar `esHiloPerdido(error): boolean` (404). En el `catch` de `useAsistente`, si `esHiloPerdido(error)` → `hilo.current = null`. El texto «Se perdió el hilo…» no cambia.
- [x] 1.5 Verde y commit `fix(asistente): …` con «Verificado en rojo: …».

## 2. fix(asistente): `AbortSignal` + timeout por turno + aborto al desmontar el dueño

- [x] 2.1 Test rojo primero — «El turno viaja con `signal` y timeout»: `mock.calls[0][2].signal` es `AbortSignal`; espiar `apiClient.post` → recibe `timeout: 160000`. Hoy falla: `consultar` no recibe opciones.
- [x] 2.2 Test rojo primero — «El timeout se muestra como error»: `consultar` rechaza con `new AxiosError("timeout", "ECONNABORTED")` → `findByText(/tardó demasiado/)`. Hoy falla: cae en el mensaje genérico de «sin respuesta».
- [x] 2.3 Test rojo primero — «Desmontar el dueño aborta el request en vuelo»: `montar(<AsistentePage/>)`, turno pendiente, `unmount()`; el `signal` capturado tiene `aborted === true`; sin warning de `act` por `setState` tras desmontar.
- [x] 2.4 `api/asistenteApi.ts`: `consultar(consulta, clave, { signal }?)` pasa `signal` y `timeout: PRESUPUESTO_DEL_TURNO_MS` (constante exportada, 160 000, comentario: 150 s del backend más margen para que corte el servidor) al `post`. Sin timeout global en `shared/api/client.ts`.
- [x] 2.5 `hooks/useAsistente.ts`: `AbortController` por turno en un ref; abortado en el cleanup del hook; el `catch` ignora `esCancelacion(error)` y no hace `setTurnos` sobre un componente desmontado.
- [x] 2.6 `errores.ts`: exportar `esCancelacion(error)` (`axios.isCancel`); separar `ECONNABORTED` (timeout) de «sin respuesta»: «El asistente tardó demasiado en responder. Probá con una pregunta más acotada.»
- [x] 2.7 Verde y commit `fix(asistente): …` con «Verificado en rojo: …».

## 3. fix(asistente): tabla ancha recortada por el `overflow: hidden` de la librería + marca de columna sensible

- [x] 3.1 Test rojo primero — `TablaDeResultado.test.tsx` (nuevo): el `Table` recibe la clase propia (`container.querySelector(".adoc-table-wrap.adoc-asistente-tabla-wrap")` no nulo). Contrato mínimo: jsdom no calcula layout.
- [x] 3.2 Test rojo primero — «Columna sensible marcada y anunciada»: fixture con `documento` sensible → `getByRole("columnheader", { name: /documento.*dato personal/i })`; la cabecera `apellido` no lo tiene; la leyenda «Las columnas con candado…» está; con ninguna sensible, no está.
- [x] 3.3 `components/TablaDeResultado.tsx`: `<Table className="adoc-asistente-tabla-wrap">`; `Table.Cell numeric` cuando `typeof valor === "number"`; `MarcaSensible` en las cabeceras con `sensible`; leyenda en `--text-caption-size` sólo si hay alguna.
- [x] 3.4 `components/MarcaSensible.tsx` (nuevo): `lockIcon` `aria-hidden` + sr-only «(dato personal)».
- [x] 3.5 `app/shell/icons.tsx`: `lockIcon` (grilla 18, trazo 1.5, como `sparkIcon`).
- [x] 3.6 `asistente.css`: `.adoc-asistente-tabla .adoc-table-wrap { overflow: auto; max-height: 50vh }`, `.adoc-asistente-tabla table.adoc-table { width: max-content; min-width: 100% }`; quitar el `overflow-x` del wrapper externo. Sin `!important`.
- [x] 3.7 Verificar a ojo en Chrome y Firefox con una respuesta de 8 columnas en el modal: scrollea en los dos ejes, no recorta.
- [x] 3.8 Verde y commit `fix(asistente): …` con «Verificado en rojo: …».

## 4. refactor(asistente): `asistente.css` a tokens, dedupe, montajes iguales, modal 880 px, responsive

- [x] 4.1 Mapear cada fallback a su token real: `--color-fg-muted`→`--color-text-secondary`, `--color-bg-subtle`→`--color-bg-sunken`, `--color-border`→`--color-border-default`/`--color-border-subtle`, `--radius-md`→`--radius-sm`, `#fff`→`--color-text-on-accent`, indigo→`--color-accent`. Borrar duplicados (`.adoc-asistente-conversacion` ×2, `.adoc-asistente-entrada` ×2, comentario repetido). Al final `grep '#' asistente.css` sólo en comentarios.
- [x] 4.2 Montajes iguales: `.adoc-asistente-modal .body { padding: 0; color: inherit; font-size: inherit }`; `.adoc-asistente-modal { max-width: min(880px, calc(100vw - 48px)) }`; panel `min(72vh, 680px)`; `title="Asistente"` en el `Modal` de `LanzadorAsistente`.
- [x] 4.3 Página: `pages/AsistentePage.tsx` envuelve en `.adoc-asistente-pagina` con `max-width: 880px; margin-inline: auto` y alto fijo (medir `.adoc-topbar` y `.adoc-page-head` en `shell.css`) para que sólo scrollee el hilo; texto de respuesta `max-width: 72ch`.
- [x] 4.4 `components/FranjaDeEstado.tsx` (nuevo): una fila entre hilo y composer con `IndicadorDeProceso` a la izquierda y `LineaDeMetricas` a la derecha, ambos intactos y fuera del log; puntos que laten con pseudo-elementos sin tocar el `textContent` «Consultando…».
- [x] 4.5 `@media (max-width: 640px)`: `.adoc-modal-stage:has(.adoc-asistente-modal) { padding: 0 }`, modal `max-width: none; width: 100%; height: 100%; border-radius: 0; border: 0`, panel `height: 100%`, burbuja `max-width: 92%`, chips a ancho completo. `@media (prefers-reduced-motion: reduce)` apaga las animaciones.
- [x] 4.6 Los tests existentes «Indicador y métricas siguen fuera del log» y «Los dos montajes muestran el mismo panel» pasan sin cambios de texto.
- [x] 4.7 Verde y commit `refactor(asistente): …`.

## 5. feat(asistente): composer (`EntradaDePregunta`, contador, «Enviar», auto-grow fallback)

- [x] 5.1 Tests — `EntradaDePregunta.test.tsx` (nuevo): «El contador aparece recién cerca del límite» (`type` de 1 799 caracteres → `queryByText(/\/ 2.?000/)` nulo; uno más → visible; el textarea tiene `maxLength` 2000); «Enter hace salto con puntero grueso» (`window.matchMedia = () => ({ matches: true, … })`; `type("a{Enter}")` → `consultar` no llamado, el valor contiene `\n`).
- [x] 5.2 Ajustar `asistente.test.tsx` «manda la pregunta y muestra la respuesta»: el botón de envío pasa de «Preguntar» a «Enviar» (renombre deliberado). El test del lanzador «Preguntar» queda intacto.
- [x] 5.3 `components/EntradaDePregunta.tsx` (nuevo): props `{ valor, onCambiar, onEnviar, enVuelo, maxCaracteres = 2000, umbralDelContador = 1800 }` + `ref` al textarea; destello; `Textarea` con `maxLength` y `aria-describedby` al contador (sin `aria-live`); botón «Enviar» con `sendIcon` y etiqueta visible, deshabilitado en vuelo o vacío, sin spinner; Enter/Shift+Enter; `matchMedia("(pointer: coarse)")` guardado si falta.
- [x] 5.4 `hooks/useAltoAutomatico.ts` (nuevo): `style.height = scrollHeight` hasta 6 líneas como fallback de `field-sizing: content`.
- [x] 5.5 `app/shell/icons.tsx`: `sendIcon`.
- [x] 5.6 `PanelAsistente.tsx` usa `EntradaDePregunta` y conserva el foco al campo al terminar el turno.
- [x] 5.7 CSS del contador y del botón de envío con tokens.
- [x] 5.8 Verde y commit `feat(asistente): …`.

## 6. feat(asistente): razonamiento + `categoria` fuera del tipo (cierra ARS-79 v1)

- [x] 6.1 Tests — `Mensaje.test.tsx` (nuevo): con `razonamiento` hay un `summary` «Cómo lo interpreté» y el `details` está cerrado; sin él, `queryByText` nulo; «Entendí:» visible fuera del `details`; «Ninguna etiqueta interna en el DOM»: tras una respuesta, `document.body.textContent` no contiene `consulta_simple`, `respondida`, `designaciones.` ni `identity.`.
- [x] 6.2 `components/Razonamiento.tsx` (nuevo): `<details class="adoc-asistente-razonamiento"><summary>Cómo lo interpreté</summary><p>…</p></details>`; `null` si vacío.
- [x] 6.3 `components/Mensaje.tsx`: orden «Entendí:» (visible, `--text-body-sm-size`, pregunta en cursiva) → marco/texto → tabla → opciones → sugerencias → pie con `Razonamiento` y «Ver la consulta».
- [x] 6.4 `types.ts`: quitar `categoria` de `MetricasDelTurno` con comentario (etiqueta interna, RNF-18); quitar `categoria` de las fixtures de `asistente.test.tsx`.
- [x] 6.5 Verde y commit `feat(asistente): …` (referenciar ARS-79 en el cuerpo).

## 7. feat(asistente): estado inicial + chips

- [x] 7.1 Tests — `EstadoInicial.test.tsx` (nuevo): «usa `descripcion` y no `nombre`» (`findByText("Los pedidos del trámite.")`; `queryByText("designaciones.pedidos")` nulo); un chip envía la pregunta; chips deshabilitados en vuelo; «Sin acceso no hay formulario» (`obtenerCapacidades` rechaza 403; `findByText(/No tenés acceso/)`; `queryByLabelText("Tu pregunta")` nulo).
- [x] 7.2 `components/EstadoInicial.tsx` (nuevo): título «¿Qué querés saber del sistema?» + línea con `alcance`; chips `<button>` pastilla (`--radius-pill`) con `ejemplos`; «Puedo consultar:» con `cubre[].descripcion` sólo cuando viene; «No puedo:» con `noPuede` compacto. Desaparece con el primer turno. El texto «N áreas de datos del sistema» que fija el test existente se conserva en la línea de alcance o se cambia el test a propósito en el mismo commit.
- [x] 7.3 `PanelAsistente.tsx`: con `tieneAcceso === false` renderiza sólo `InlineAlert info` «No tenés acceso al asistente con tus permisos actuales.», sin campo ni botón.
- [x] 7.4 `Opciones.tsx` / `Sugerencias.tsx`: sólo clases (barra de acento a la izquierda / chips pastilla `ghost`); textos intactos.
- [x] 7.5 Verde y commit `feat(asistente): …`.

## 8. feat(asistente): reintentar, «Dejar de esperar», nueva conversación, conversación que sobrevive al cierre, foco del modal

- [x] 8.1 Tests — `asistente.turnos.test.tsx`: «Reintentar reusa la clave» (`consultar` rechaza una vez y luego resuelve; clic «Reintentar»; `mock.calls[0][1] === mock.calls[1][1]`); «Reintentar no aparece en vuelo ni en un turno que se dejó de esperar»; «El timeout ofrece Reintentar»; «Nueva conversación vacía el hilo» (un turno; clic; la respuesta ya no está; el próximo `consultar` va con `hilo: null`; foco en el campo); «Nueva conversación deshabilitado sin turnos»; «Dejar de esperar aborta y libera el campo» (`consultar` con promesa que rechaza con `CanceledError` al abortar; clic; `findByText(/Dejaste de esperar/)`; sin `role="alert"`; «Enviar» habilitado; foco en el campo; «Dejar de esperar» ya no está).
- [x] 8.2 Tests — `LanzadorAsistente.test.tsx` (nuevo): «El foco vuelve al lanzador al cerrar» (abrir, `user.keyboard("{Escape}")`, `expect(boton).toHaveFocus()`); «El modal contiene el foco» (`#root` creado en el test tiene `inert` abierto y no lo tiene cerrado); «La conversación sobrevive al cierre» (abrir, preguntar, cerrar, abrir → la respuesta sigue en el log); «Cerrar el modal NO aborta» (turno pendiente, Esc; `signal.aborted === false`; resolver; reabrir → la respuesta está).
- [x] 8.3 `hooks/useAsistente.ts`: `reintentar(id)` (mismo texto, misma clave `id`; sólo si el turno tiene `error`); `reiniciar()` (turnos `[]`, `hilo.current = null`, aborta el turno en vuelo si lo hay); `detener()` (aborta el controller, marca `detenido: true`, no es error). Interfaz `Asistente { turnos, enVuelo, preguntar, reintentar, reiniciar, detener }`. `types.ts`: `TurnoDeLaConversacion.detenido?: boolean`.
- [x] 8.4 `api/asistenteApi.ts`: el docstring sobre el reintento ahora es cierto; ajustar la redacción si hace falta.
- [x] 8.5 Estado al dueño del montaje: `LanzadorAsistente` y `AsistentePage` invocan `useAsistente` y lo pasan a `PanelAsistente` por prop `asistente`; `PanelAsistente` gana `mostrarEncabezado?`; quitar el comentario de `LanzadorAsistente` («montarlo siempre dejaría un hilo abierto») que dejó de aplicar.
- [x] 8.6 `components/Mensaje.tsx`: en error, `Button secondary sm` «Reintentar» (`onReintentar(turno.id)`); turno `detenido` → texto en `--color-text-secondary` «Dejaste de esperar la respuesta. La consulta ya salió y cuenta para tu cupo.», sin `InlineAlert` y sin «Reintentar». `Conversacion.tsx` pasa `onReintentar`; opcional `aria-busy={enVuelo}`.
- [x] 8.7 `components/FranjaDeEstado.tsx`: botón «Dejar de esperar» (`ghost sm`, `stopIcon`) sólo mientras `enVuelo`; sin tooltip ni sr-only que prometa ahorro.
- [x] 8.8 «Nueva conversación» (`ghost sm`, `plusIcon`; deshabilitado sin turnos o en vuelo; sin confirmación; foco al campo): en la página en `PageHeader actions`; en el modal en el encabezado del panel.
- [x] 8.9 `components/LanzadorAsistente.tsx`: `ref` al botón + `focus()` al cerrar; `document.getElementById("root")?.setAttribute("inert", "")` mientras abierto, quitado al cerrar.
- [x] 8.10 `app/shell/icons.tsx`: `plusIcon`, `stopIcon`.
- [x] 8.11 Verde y commit `feat(asistente): …`.

## 9. feat(asistente): scroll anclado al inicio de la respuesta + «Ir al final»; tabla sticky; copiar

- [ ] 9.1 Tests — `useAnclaAlFinal.test.tsx` (nuevo): «El hilo no arrastra si el usuario subió» (`Object.defineProperty(hilo, "scrollHeight", { value: 1000 })`, `clientHeight` 300, `scrollTop` 0; `fireEvent.scroll(hilo)`; llega una respuesta; `scrollTop` sigue en 0 y aparece «Ir al final»; clic → `scrollTop === scrollHeight - clientHeight`); «Anclado abajo, sí sigue» (`scrollTop = 700`; tras la respuesta queda en el inicio de la tarjeta, `offsetTop` fingido; sin botón).
- [ ] 9.2 Tests — `utils/portapapeles.test.ts` (nuevo): `tablaComoTsv` produce cabecera + filas separadas por tab. `Mensaje.test.tsx`: «Copiar respuesta escribe en el portapapeles» (`userEvent.setup()` instala el stub; clic; `await navigator.clipboard.readText()` === texto; etiqueta «Copiado»); «Sin portapapeles no hay botón» (`Object.defineProperty(navigator, "clipboard", { value: undefined })` → `queryByRole("button", { name: /Copiar/ })` nulo).
- [ ] 9.3 `hooks/useAnclaAlFinal.ts` (nuevo): `onScroll`, anclado ≤ 24 px; al enviar → fondo; respuesta con anclado → `scrollTop = tarjeta.offsetTop - hilo.offsetTop`; no anclado → no mover; `irAlFinal()` con `auto`. Reemplaza el salto ciego de `PanelAsistente`.
- [ ] 9.4 `components/IrAlFinal.tsx` (nuevo): `Button secondary sm` con `arrowDownIcon`, flotante abajo a la derecha del hilo, fuera del `<ul role="log">`.
- [ ] 9.5 `utils/portapapeles.ts` (nuevo): `tablaComoTsv(columnas, filas)` y `copiar(texto)`, puras.
- [ ] 9.6 `components/AccionesDelMensaje.tsx` (nuevo): «Copiar respuesta» y, si hay tabla, «Copiar tabla», `ghost sm` con `copyIcon`, siempre visibles, «Copiado» dos segundos; `null` si `!navigator.clipboard`. Se monta en el pie de `Mensaje`.
- [ ] 9.7 `asistente.css`: `.adoc-asistente-tabla thead th { position: sticky; top: 0; z-index: 1 }`; botón flotante con transición apagada bajo `prefers-reduced-motion`.
- [ ] 9.8 `app/shell/icons.tsx`: `copyIcon`, `arrowDownIcon`.
- [ ] 9.9 Verde y commit `feat(asistente): …`.

## 10. docs(asistente): domains/asistente.md y tech-debt

- [ ] 10.1 `docs/architecture/domains/asistente.md` §La superficie de usuario: «la abre en un cajón» → modal centrado; sumar razonamiento colapsado, reintento con la misma clave (sólo en error, y por qué), «Dejar de esperar» y su límite, conversación nueva, conversación que sobrevive al cierre, foco (retorno + `inert`), marca de columna sensible. §Specs activas: agregar `openspec/changes/asistente-rediseno-conversacion/`.
- [ ] 10.2 `docs/quality/tech-debt.md`: TD-014 `Modal` de `@ars-docendi/ui` sin focus trap ni retorno de foco (workaround `inert` sobre `#root` en la feature; quitarlo cuando la librería lo traiga); TD-015 dos sets de íconos SVG (`app/shell/icons.tsx` grilla 18 y `features/designaciones/components/lucide.tsx` grilla 24), unificar en `shared/ui/iconos/` cuando designaciones no esté en edición viva.
- [ ] 10.3 `docs/product/designs/asistente-conversacional-design-spec.md` ya existe (se creó con este change): revisar que lo implementado coincida y pasar `status` a `review`.
- [ ] 10.4 Prettier y commit `docs(asistente): …`.
