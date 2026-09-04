## Contexto

La feature vive en `frontend/src/features/asistente/` con una sola vista, `PanelAsistente`, montada dos veces: la ruta `/asistente` y el modal que abre el lanzador «Preguntar» de la barra superior. El estado de la conversación (`turnos`, `enVuelo`, `hilo`) lo crea `useAsistente` **dentro del panel**, y el panel se monta recién al abrir el modal: cerrar destruye el hilo.

Lo que se preserva porque está bien y fijado por spec: lanzador con etiqueta visible; modal centrado y no cajón; pregunta en burbuja de acento a la derecha y respuesta en tarjeta a lo ancho (trae tablas y menús); sólo scrollea el hilo; Enter envía y el botón se queda; región viva acotada a la lista con `role="log"`, indicador `role="status"` con umbral y **sin etapas**, métricas fuera; degradado como estado; opciones ≠ sugerencias; truncado sin números; gate por 403 real; clave de idempotencia por intento.

Restricciones duras: sin fake UI (invariante #7); código y copy en español (#13); RNF-17 y RNF-18; tokens sólo de `@ars-docendi/ui/dist/theme.css`; íconos SVG a mano en `app/shell/icons.tsx`; sin cross-imports entre features; el backend no cambia.

Del backend importan cuatro hechos verificados: acepta `hilo` nulo como conversación nueva; limita `mensaje` a 2 000 caracteres; el turno tiene un presupuesto de 150 s; y la idempotencia (`IdempotenciaEnMemoria.cs`, `AsistenteController.cs:53-76`) consulta la caché **antes** de ejecutar el turno y guarda **después**, sin registrar el turno en curso: un reintento con la misma clave mientras el original sigue en vuelo ejecuta el turno completo por segunda vez.

## Goals / Non-Goals

**Goals:**

- Cerrar los defectos funcionales: turno concurrente por Enter, bucle de 404, request colgado, tabla recortada, columna sensible ignorada, formulario sin acceso.
- Sumar sólo lo que tiene backend hoy: reintentar, dejar de esperar, conversación nueva, conversación que sobrevive al cierre, razonamiento, copiar, scroll anclado.
- Que la superficie use el tema institucional y se vea igual en los dos montajes, también en un teléfono.
- Cerrar ARS-79 (variante 1).

**Non-Goals:**

- Streaming, etapas simuladas, regenerar, feedback, editar, adjuntar, historial, `localStorage`, Markdown en las respuestas, conteo de filas.
- Tocar el backend, la librería `@ars-docendi/ui` o las specs del change `asistente-frontend`.
- Unificar la conversación de la ruta y la del modal.

## Decisiones

### D1 — El estado de la conversación vive en el dueño del montaje

`useAsistente` se invoca en `LanzadorAsistente` (modal) y en `AsistentePage` (ruta); `PanelAsistente` recibe `asistente: Asistente` por prop. El lanzador vive con la barra superior, así que la conversación del modal sobrevive a cerrar y reabrir; el panel sigue montándose sólo al abrir y no hay ningún pedido al backend hasta la primera pregunta.

**Alternativa descartada:** mantener el hook en el panel y no desmontarlo al cerrar. El `Modal` de la librería devuelve `null` cuando `open` es `false`, así que los hijos se desmontan igual y el estado se pierde. **Alternativa descartada:** un contexto en `AppLayout` para compartir el hilo entre ruta y modal — cuesta lo mismo más un provider y una decisión de producto que no está tomada. Página y modal siguen siendo hilos independientes.

Sin `localStorage`: guardar en el navegador filas con datos personales sin política de retención contradice §3.4 de la definición. Sigue muriendo al recargar, como decidió D3 del change anterior.

### D2 — Un solo estado de proceso con umbral, sin etapas

Se mantiene «Consultando…» a los 400 ms como único contenido textual; se le agregan tres puntos que laten por CSS con pseudo-elementos (no alteran `textContent`), apagados con `prefers-reduced-motion`. Etapas reales exigen SSE (definición §4.6); etapas por temporizador son fake UI. El indicador y la línea de métricas se funden en una **franja de estado** de una fila entre el hilo y el composer, ambos fuera del log: la posición cambia, el contrato de accesibilidad no.

### D3 — `razonamiento` va dentro del mensaje, colapsado

`<details>` nativo con resumen «Cómo lo interpreté», después del texto y la tabla, al lado de «Ver la consulta» (mismo patrón). Es parte de la respuesta, así que va **en la región viva**; el contenido de un `<details>` cerrado no se anuncia hasta abrirlo, así que no agrega ruido al lector. Renderizado condicional: sin razonamiento no queda hueco. `preguntaInterpretada` queda **visible** como «Entendí: …» (RF-10): esconderla derrota el aviso de que se reinterpretó la pregunta.

### D4 — `metricas.categoria` sale del tipo; `cubre[].nombre` no se muestra

Son etiquetas internas (`consulta_simple`, `cruce_de_tablas`, `designaciones.pedidos`) y RNF-18 las prohíbe. `categoria` se quita de `MetricasDelTurno` con un comentario que diga por qué; el backend la sigue mandando y el cliente la ignora. El estado inicial usa `cubre[].descripcion` sólo cuando viene.

### D5 — Reintentar reusa la clave, y sólo se ofrece cuando el turno terminó en error

`reintentar(idTurno)` reenvía el mismo texto con la misma `Idempotency-Key` (`turno.id`): es el uso documentado de la clave y no factura dos veces al modelo cuando el backend ya terminó. Si el error fue 404, `hilo.current` se limpia antes (cierra el bucle). El docstring de `asistenteApi.ts:12-15` pasa a ser cierto.

**Regla que sale de leer el backend:** «Reintentar» **nunca** se ofrece en vuelo ni sobre un turno que el usuario dejó de esperar. Con la idempotencia en memoria consultando antes y guardando después, un segundo pedido con la misma clave mientras el original corre en el servidor ejecuta el turno entero de nuevo y el último `Guardar` pisa al primero. El botón sólo aparece en turnos con `error`; tras «Dejar de esperar» el usuario vuelve a escribir, con clave nueva, sabiendo que la primera consulta ya se cobró.

### D6 — El cliente nunca queda colgado; «Dejar de esperar» es honesto

`consultar(consulta, clave, { signal })` acepta un `AbortSignal` y fija `timeout: PRESUPUESTO_DEL_TURNO_MS = 160_000` **por request**, no en `shared/api/client.ts` (el resto de la app no tiene turnos de 150 s). El margen sobre los 150 s del backend es para que el que corte sea el servidor con su mensaje de degradado, y el cliente sólo como red de seguridad. `ECONNABORTED` deja de ser inalcanzable y se muestra como error con «Reintentar»: «El asistente tardó demasiado en responder. Probá con una pregunta más acotada.»

`useAsistente` guarda el `AbortController` del turno en vuelo y lo aborta en el cleanup del hook, que corre al desmontarse el **dueño** (D1): navegar fuera de `/asistente` aborta; cerrar el modal **no** aborta, la respuesta llega y espera al reabrir.

El botón se llama **«Dejar de esperar»** (no «Detener», no «Cancelar»): aborta el request del cliente y libera el campo; el backend sigue procesando hasta su presupuesto y cobra la cuota. El turno queda marcado `detenido` con «Dejaste de esperar la respuesta. La consulta ya salió y cuenta para tu cupo.» en texto secundario, sin `InlineAlert`, y el foco vuelve al campo. Un aborto a pedido (`ERR_CANCELED`) no es error.

### D7 — El composer guarda `enVuelo` en el hook, no sólo en la UI

`preguntar` ignora el envío si ya hay un turno en vuelo, y el `onKeyDown` tampoco llama a `enviar`. El guard vive en el hook para que ningún montaje futuro lo pierda. Se puede seguir escribiendo mientras tanto. `maxLength={2000}` con contador «1 850 / 2 000» visible recién desde 1 800, ligado con `aria-describedby` y sin `aria-live` (un contador vivo es ruido). Auto-crecimiento hasta 6 líneas con `field-sizing: content` más `useAltoAutomatico` como fallback para Firefox/Safari. Con `matchMedia("(pointer: coarse)")` Enter inserta salto y el botón envía; se guarda la ausencia de `matchMedia` en jsdom.

### D8 — «Enviar» con etiqueta visible; el lanzador sigue «Preguntar»

Un ícono solo es el anti-patrón #6 de los principios, y Enter es un atajo, no la única vía (lector de pantalla, móvil). Se renombra el envío a «Enviar» con `sendIcon` delante para no tener dos botones «Preguntar» en el DOM con el modal abierto. Sin spinner en el botón: parpadea en las respuestas deterministas, que es lo que el umbral evita (D5 del change anterior).

### D9 — La tabla scrollea dentro de su propio marco, sin forkear la librería

El envoltorio `Table` acepta `className` y lo aplica junto a `adoc-table-wrap`. Se pasa `className="adoc-asistente-tabla-wrap"` y se sobreescribe con dos clases, que le ganan en especificidad sin `!important`: `.adoc-asistente-tabla .adoc-table-wrap { overflow: auto; max-height: 50vh }` y `.adoc-asistente-tabla table.adoc-table { width: max-content; min-width: 100% }`. Cabecera pegajosa (`thead th { position: sticky; top: 0 }`; la librería ya le da fondo). `Table.Cell numeric` cuando el valor es número. **Alternativa descartada:** `Table.Root` sin el `Table` envolvente — pierde borde y fondo del sistema y obliga a replicarlos.

### D10 — Las columnas sensibles se marcan

`columnas[].sensible` ya viene. En la cabecera, `lockIcon` `aria-hidden` más sr-only «(dato personal)»; bajo la tabla, leyenda «Las columnas con candado contienen datos personales.» sólo si hay alguna. Se dice qué es personal, no por dónde viajó (RNF-18).

### D11 — Scroll anclado, con tres reglas

`useAnclaAlFinal(refHilo)` registra `onScroll` y considera anclado estar a ≤ 24 px del fondo. (1) Al enviar: al fondo siempre. (2) Al llegar la respuesta, anclado: al **inicio de la tarjeta** (`scrollTop = tarjeta.offsetTop - hilo.offsetTop`), no al fondo — con una tabla larga saltar al fondo deja el texto de la respuesta fuera de vista. (3) No anclado: no se lo mueve; aparece «Ir al final» (`Button secondary sm`, `arrowDownIcon`) fuera del log, que baja con `auto`, no `smooth`. Reemplaza el salto ciego actual.

### D12 — Sólo acciones reales por mensaje: copiar

`AccionesDelMensaje` con «Copiar respuesta» y, si hay tabla, «Copiar tabla» (TSV con cabecera) vía `navigator.clipboard.writeText`. Siempre visibles (el hover no existe con teclado ni touch), «Copiado» dos segundos. Si `navigator.clipboard` no existe, **no se renderiza**. Regenerar, feedback, editar y adjuntar no tienen backend.

### D13 — Foco: retorno al lanzador e `inert` sobre `#root`

El `Modal` de la librería no gestiona foco (cero referencias a `focus` en el bundle). En `LanzadorAsistente`: `ref` al botón y `focus()` cuando `abierto` pasa a `false`; mientras está abierto, `#root` lleva `inert` — el `Modal` se portalea a `body`, así que `#root` es hermano y Tab queda contenido sin implementar un trap a mano. Se registra en tech-debt que el `Modal` debería traer focus trap y retorno propios; cuando lo traiga, se quita el workaround. `title="Asistente"` en el `Modal` da `aria-labelledby` real y un encabezado visible.

### D14 — Sin acceso, sólo el aviso

Con `tieneAcceso === false`, `PanelAsistente` renderiza únicamente el `InlineAlert info` «No tenés acceso al asistente con tus permisos actuales.»: un campo con botón que rechaza al enviar es un formulario que aparenta funcionar (invariante #7).

### D15 — Tokens del tema; página y modal iguales; móvil a pantalla completa

`asistente.css` se reescribe mapeando cada fallback a su token real (`--color-text-secondary/tertiary`, `--color-bg-sunken/surface/raised`, `--color-border-default/subtle`, `--radius-xs/sm/pill`, `--color-accent`, `--color-accent-subtle`, `--color-text-on-accent`, `--text-body-sm-size`, `--text-caption-size`, `--focus-ring`, `--space-*`) y borrando los bloques duplicados; al final ningún hex fuera de comentarios. `.adoc-asistente-modal .body { padding: 0; color: inherit; font-size: inherit }` anula lo que el `Modal` impone; el panel tiene alto fijo en ambos montajes (`min(72vh, 680px)` en el modal; en la página, `calc(100dvh - …)` midiendo topbar y `PageHeader`) para que sólo scrollee el hilo. Ancho de lectura 880 px, texto a 72ch, tabla a ancho completo. `@media (max-width: 640px)`: pantalla completa sin radio ni margen. `@media (prefers-reduced-motion: reduce)` para toda animación nueva.

### D16 — Seis íconos SVG a mano en `app/shell/icons.tsx`

`sendIcon`, `copyIcon`, `arrowDownIcon`, `plusIcon`, `stopIcon`, `lockIcon` en la grilla 18 / trazo 1.5 de `sparkIcon`. `lucide-react` no es dependencia y seis glifos no justifican una tercera convención; `features/designaciones/components/lucide.tsx` no se puede importar (cross-feature). Queda en tech-debt que hay dos sets.

### D17 — Copy en voseo rioplatense

Coherente con el backend («Empezá una conversación nueva») y la definición. Nuevos: «¿Qué querés saber del sistema?», «Entendí:», «Cómo lo interpreté», «Copiar respuesta», «Copiar tabla», «Copiado», «Reintentar», «Nueva conversación», «Ir al final», «Enviar», «Dejar de esperar», «Dejaste de esperar la respuesta. La consulta ya salió y cuenta para tu cupo.», «(dato personal)», «Las columnas con candado contienen datos personales.». Los textos que los tests fijan («Consultando…», «Elegí una para continuar:», «Probá con alguna de estas:», «Hay más resultados», «consultas al modelo», «El asistente no está disponible ahora») no cambian.

### D18 — OpenSpec: capability nueva, sin tocar las anteriores

`asistente-frontend` figura Complete sin archivar y sus specs no están en `openspec/specs/`; un delta MODIFIED no tendría base. Todo va como `## ADDED Requirements` en `asistente-conversacion`.

## Riesgos / Trade-offs

- **Doble cobro tras «Dejar de esperar»** → aceptado y dicho en el copy: el backend no cancela el turno. No se ofrece «Reintentar» sobre ese turno (D5); el usuario decide si vuelve a preguntar.
- **Reintento con la misma clave mientras el servidor sigue procesando** → el botón sólo existe en turnos con `error`, que llegan cuando el request terminó (red, 5xx, 404, timeout de 160 s > 150 s del backend). No hay ruta de UI que lo dispare en vuelo.
- **`inert`, `:has`, `field-sizing`, `color-mix` fijan un piso de navegador moderno** → `inert` está en los navegadores actuales; `field-sizing` tiene fallback JS (`useAltoAutomatico`); `:has` sólo afecta el padding del stage en móvil. Se anota en tech-debt que el `Modal` debería resolver el foco por sí mismo.
- **jsdom no calcula layout** → el override del `overflow` y el sticky no se pueden afirmar con RTL; se fija el contrato mínimo (la clase llega al envoltorio) y se verifica a ojo en Chrome y Firefox con ocho columnas. El scroll anclado se testea fingiendo `scrollHeight`/`clientHeight`/`offsetTop`.
- **Los overrides dependen de los nombres de clase de la librería** (`.adoc-table-wrap`, `.adoc-modal-stage`, `.body`) → un bump de `@ars-docendi/ui` puede romperlos en silencio; el test del contrato mínimo y la verificación visual del PR son la red. No se forkea ni se parchea la librería.
- **Renombrar el botón a «Enviar» cambia un test** → deliberado y en el mismo commit.
- **`asistente.test.tsx` ya tiene 379 líneas** → los tests nuevos van en archivos hermanos por componente/hook, respetando el cap de ~300.

## Plan de migración / rollback

Sin migraciones ni cambios de contrato. Diez commits chicos en el orden de `tasks.md`, cada uno verde y revertible por separado; los tres primeros son fixes red-green. Rollback: `git revert` del rango.

## Open Questions

- Los umbrales (contador a 1 800, anclaje a 24 px, timeout a 160 s) se eligieron por razonamiento, no por medición; ajustar con uso real.
- Mockup en la herramienta de diseño cuando el equipo la elija (`designs/README.md`); por ahora el design spec en texto es la fuente.
