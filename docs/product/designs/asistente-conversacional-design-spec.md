---
status: review
owner: "Equipo Ars Docendi"
feature: "openspec/changes/asistente-rediseno-v3/"
last_updated: 2026-09-26
---

# Design spec: Asistente conversacional — superficie de conversación

## Resumen

Se rediseña la interfaz con la que los roles no-docente consultan el sistema en lenguaje natural:
la pantalla inicial, el hilo de mensajes, la redacción de la pregunta y el modal desde la barra
superior. El objetivo es que la experiencia esté a la altura de los asistentes que el usuario ya
conoce (Claude, ChatGPT) **sin prometer nada que el backend no haga**: sin streaming, sin etapas
simuladas, sin regenerar, sin feedback, sin adjuntos. Cada control visible tiene una acción real
hoy. La definición funcional vive en `asistente-conversacional-definicion.md` (§3.1 RF-05, RF-10,
RF-11, RF-14, RF-15; §3.2 RNF-17, RNF-18; §4.6, §4.7).

> **Rediseño v3 (2026-09-26, `asistente-rediseno-v3`, épica ARS-140).** La superficie vigente
> es la de § «Rediseño v3 — modal con historial integrado», al final de este documento: un
> solo montaje (el modal), historial en un rail fijo, sin sugerencias salvo en la bienvenida.
> Las secciones anteriores quedan como registro de las decisiones que siguen valiendo; donde
> se contradicen con § v3, gana § v3.

## Roles que ven esta surface

- [x] Jefe de Cátedra
- [x] Coordinador de Carrera
- [x] Secretaría Académica
- [x] Decanato
- [x] Administrativos
- [ ] Docente

La visibilidad no se decide por rol sino por el permiso `asistente.consultar`, consultando
`GET /api/asistente/capacidades` (403 = no se ve nada, ni lanzador ni ruta útil).

## Flujo principal

1. Desde cualquier pantalla, el usuario pulsa **«Preguntar»** en la barra superior (pastilla con
   destello). Se abre un modal centrado titulado «Asistente» con el foco en el campo de pregunta.
   (Desde v3 no hay página `/asistente`: un vínculo viejo redirige a la home con el modal abierto.)
2. Ve el **estado inicial**: título «¿Qué querés saber del sistema?», una presentación escrita para
   su rol —por qué cosas suele venir a preguntar—, debajo y en secundario el alcance de sus datos
   con cuántas áreas conoce el asistente, chips con preguntas de ejemplo verificadas y qué no puede
   hacer el asistente.
3. Escribe la pregunta (Enter envía; Shift+Enter hace salto de línea; en pantallas táctiles Enter
   hace salto y se envía con **«Enviar»**) o pulsa un chip. La pregunta aparece en burbuja de
   acento a la derecha y el hilo se desplaza para mostrarla.
4. Si la respuesta tarda más de 400 ms aparece **un solo estado** «Consultando…» en la franja de
   estado, fuera de la conversación, junto al botón **«Dejar de esperar»**. No hay etapas.
5. Llega la respuesta en tarjeta a lo ancho: si hubo reinterpretación, primero «Entendí: …»; luego
   el texto; la tabla de resultados si la hay (con aviso «Hay más resultados…» si se truncó); las
   opciones de aclaración (desde v3, sin sugerencias); y al pie, colapsados, «Cómo lo interpreté»
   (razonamiento) y «Ver la consulta» (sólo con `asistente.ver_consulta`), más «Copiar respuesta»
   / «Copiar tabla».
6. El foco vuelve al campo. El hilo queda en el inicio de la respuesta. Si el usuario había subido
   a releer, no se lo arrastra: aparece «Ir al final».
7. Puede seguir preguntando sobre el mismo tema (el hilo viaja solo) o pulsar **«Nueva
   conversación»** para empezar de cero.
8. Cierra el modal con Esc, la «×» o clic afuera; el foco vuelve a «Preguntar». La conversación
   se conserva mientras dure la sesión de la página: al reabrir, sigue donde estaba.

## Layout / IA

**Panel (idéntico en los dos montajes):**

```
┌ Encabezado del panel ───────────────────────── [＋ Nueva conversación] ┐
│ Hilo (scrollea solo)                                                    │
│   • Estado inicial (sólo sin turnos)                                    │
│   • Turno: burbuja usuario (derecha, ≤ 82 %)                            │
│            tarjeta respuesta (ancho completo)                           │
│                                          [↓ Ir al final] (flotante)     │
├ Franja: «Consultando…» [■ Dejar de esperar] …… «2 consultas al modelo.» ┤
│ ✦ [ Escribí tu pregunta…                               ] [➤ Enviar]     │
│                                               1 850 / 2 000 (≥ 1 800)   │
└────────────────────────────────────────────────────────────────────────┘
```

- **Modal**: `max-width` 880 px, alto del panel `min(72vh, 680px)` —el `vh` dividido por el
  zoom de la interfaz, como hace el escenario del modal—, título «Asistente» en el encabezado
  del `Modal`. En ≤ 640 px ocupa la pantalla completa, sin radio ni margen.
- **Página `/asistente`**: eliminada en v3 (ARS-151); ver § Rediseño v3.
- **Tarjeta de respuesta**: fondo `--color-bg-sunken`, radio `--radius-sm`, texto a `72ch`; la
  tabla ocupa todo el ancho de la tarjeta, con cabecera pegajosa y `max-height: 50vh`.
- **Celda con vínculo**: la celda que identifica algo abrible se pinta como enlace
  (`--color-accent`, subrayado con `text-underline-offset: 2px`). **No hay columna «Ver»**: en el
  modal el ancho ya está comprometido y el identificador es lo que la mano iba a buscar de todos
  modos. Nombre accesible «Ver el trámite 2026-9005», no el número solo.
- **Burbuja del usuario**: `--color-accent` / `--color-text-on-accent`, alineada a la derecha.
- **Opciones de aclaración**: bloque con barra de acento a la izquierda, botones `secondary`.
- **Sugerencias**: eliminadas en v3 salvo los ejemplos de la bienvenida (ARS-149).
- **Sin mockup** por ahora (herramienta de diseño TBD, ver `README.md` de esta carpeta). Si se
  hace uno, va en `exports/asistente-conversacional/`.

## Estados a diseñar

| Estado              | Descripción                                                                                                                                                                                  | Cuándo se muestra                                                              |
| ------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| Cargando acceso     | No hay lanzador; `/asistente` muestra el panel sin estado inicial hasta que el catálogo responda                                                                                             | Primera carga de la app                                                        |
| Sin acceso          | No hay lanzador; `/asistente` muestra sólo un `InlineAlert info` «No tenés acceso al asistente con tus permisos actuales.», sin campo ni botón                                               | 403 del backend                                                                |
| Inicial (vacío)     | Título + presentación según el rol + alcance con el conteo de áreas + chips de ejemplos + límites; campo con foco                                                                            | Sin turnos                                                                     |
| En vuelo            | Pregunta ya en el hilo; «Enviar» deshabilitado; Enter no envía pero se puede escribir; chips deshabilitados; a los 400 ms «Consultando…» + «Dejar de esperar»                                | Entre el envío y la respuesta                                                  |
| Respondida          | Tarjeta con texto (+ «Entendí:» si aplica), tabla, sugerencias, disclosures, copiar                                                                                                          | `estado = respondida`                                                          |
| No contestable      | Texto del backend, sin chips (v3)                                                                                                                                                            | `estado = no_contestable`                                                      |
| Necesita aclaración | `InlineAlert info` «Necesito que precises algo» + opciones que continúan el turno                                                                                                            | `estado = necesita_aclaracion`                                                 |
| Servicio degradado  | `InlineAlert warning` «El asistente no está disponible ahora» + texto del backend (cupo propio, tope organizacional, turno concurrente, proveedor caído o mantenimiento); nunca rojo         | `estado = servicio_degradado`                                                  |
| Error de transporte | `InlineAlert danger` «No se pudo consultar» + mensaje en español + **«Reintentar»** (misma clave de idempotencia). Si fue 404, el hilo se reinicia solo                                      | Red, 5xx, 404 — siempre con el request ya terminado                            |
| Tiempo agotado      | Mismo `InlineAlert danger` con «El asistente tardó demasiado en responder. Probá con una pregunta más acotada.» + «Reintentar»                                                               | El cliente cortó a los 160 s (por encima del presupuesto de 150 s del backend) |
| Dejó de esperar     | Bajo la pregunta, en texto secundario: «Dejaste de esperar la respuesta. La consulta ya salió y cuenta para tu cupo.» Sin alerta y **sin «Reintentar»**. El campo se libera y recibe el foco | El usuario pulsó «Dejar de esperar»                                            |
| Columna sensible    | Candado junto al nombre de la columna + «(dato personal)» sr-only; leyenda bajo la tabla «Las columnas con candado contienen datos personales.»                                              | `columnas[i].sensible = true`                                                  |
| Truncado            | Bajo la tabla: «Hay más resultados de los que se muestran. Acotá la pregunta para verlos.» Sin números                                                                                       | `truncado = true`                                                              |
| Cerca del límite    | Contador «1 850 / 2 000» junto al campo, sin región viva                                                                                                                                     | ≥ 1 800 caracteres                                                             |
| Desplazado          | Botón flotante «Ir al final»                                                                                                                                                                 | El usuario subió y llegó algo nuevo                                            |
| Copiado             | La etiqueta del botón pasa a «Copiado» 2 s                                                                                                                                                   | Tras copiar                                                                    |

No aplica «Awaiting approval».

## Copy

Voseo rioplatense, coherente con el backend («Empezá una conversación nueva») y con la definición:
«Escribí tu pregunta…», «Probá con alguna de estas:», «Elegí una para continuar:», «Acotá la
pregunta». Nuevos: «¿Qué querés saber del sistema?», «Entendí:», «Cómo lo interpreté», «Ver la
consulta», «Copiar respuesta», «Copiar tabla», «Copiado», «Reintentar», «Nueva conversación», «Ir
al final», «Enviar», «Consultando…», «Dejar de esperar», «Dejaste de esperar la respuesta. La
consulta ya salió y cuenta para tu cupo.», «El asistente tardó demasiado en responder. Probá con
una pregunta más acotada.», «(dato personal)», «Las columnas con candado contienen datos
personales.». Ningún texto contiene códigos HTTP, nombres de tablas, valores de `estado` ni
`metricas.categoria`.

**La presentación del estado inicial la escribe el backend, una por rol**, y llega en
`capacidades.presentacion`. El cliente no tiene ninguna copy de rol: `identity.roles` no es un
catálogo cerrado —Secretaría crea roles desde la aplicación— así que una tabla embebida en el
cliente se desactualizaría sola. Todas están en modo consulta, porque el asistente sólo consulta:

| Rol                   | Presentación                                                                                                                               |
| --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `jefe_catedra`        | «Preguntá por las designaciones y los pedidos de tu cátedra: quién está designado, en qué materia y en qué estado quedó cada trámite.»     |
| `coordinador_carrera` | «Preguntá por los pedidos de tu carrera: qué hay pendiente de revisión, en qué estado está cada trámite y quién quedó designado.»          |
| `secretaria`          | «Preguntá por cualquier cátedra del Departamento: designaciones, pedidos, períodos y cómo viene el trámite en cada carrera.»               |
| `decanato`            | «Preguntá por cómo viene el trámite en todo el Departamento: qué llegó a la aprobación final, qué quedó pendiente y quién está designado.» |
| `administrativo`      | «Preguntá por los datos del trámite y los catálogos del sistema: períodos, cargos, materias y en qué estado está cada pedido.»             |
| `docente`             | «Preguntá por tus designaciones: en qué materias estás designado, con qué cargo y desde cuándo.»                                           |
| genérica              | «Preguntá por las designaciones, los pedidos y los períodos del sistema.»                                                                  |

La genérica es la de quien tiene **varios** roles, **ninguno**, o uno que la tabla no conoce. No hay
tabla de precedencia: inventar que «secretaria gana a jefe_catedra» sería fabricar una jerarquía que
nadie pidió para elegir un saludo, y un genérico correcto es mejor que un específico adivinado.

## Decisiones de diseño

- **Un solo estado de proceso con umbral, sin etapas.** Etapas reales exigen SSE (definición
  §4.6); etapas por temporizador son fake UI (invariante #7). Se mantiene «Consultando…» a los
  400 ms; se le agregan puntos que laten por CSS, apagados con `prefers-reduced-motion`.

- **Espera pareja para los turnos que no llaman al modelo.** Los carriles deterministas
  contestan en milisegundos y el carril SQL en segundos: un orden de magnitud. Una respuesta
  instantánea después de otra que tardó cinco segundos no se lee como «fue rápido», se lee como
  «no hizo nada» o «no me entendió». El cliente retiene esas respuestas hasta que el turno se
  parezca a uno con modelo.

  Tres cosas la mantienen honesta y la separan del progreso simulado que la lista de
  anti-patterns prohíbe. **Uno**: no inventa etapas — el indicador sigue diciendo una sola cosa
  cierta, y lo único que cambia es cuándo aparece la respuesta, no qué dice. **Dos**: el número
  sale de los turnos reales de la sesión —el cliente promedia los últimos cinco que sí llamaron
  al modelo— y no de una constante, así que se adapta al proveedor y a la red del día. **Tres**:
  el retardo vive en el cliente, nunca en el servidor, porque `latencia_ms` del registro
  operativo mide trabajo real y un retardo del lado del backend la habría corrompido.

  Acotada entre 1 s y 2,5 s. El piso no es arbitrario: con el indicador apareciendo a los
  400 ms, una espera más corta lo haría parpadear, que es exactamente lo que ese umbral existe
  para evitar. El techo impide que un día lento del proveedor convierta un saludo en una espera
  de ocho segundos.

  **Los errores y el servicio degradado quedan afuera: llegan al instante.** La espera pareja
  empareja respuestas; hacer esperar a alguien para darle una mala noticia es coherencia que no
  vale lo que cuesta.

- **`razonamiento` va dentro del mensaje, colapsado, y sólo en modo debug** (cierra ARS-79
  variante 1, RF-11; acotado por asistente-razonamiento-solo-en-debug). Un `<details>` nativo con
  resumen «Cómo lo interpreté»: parte de la respuesta, en la región viva, y no se anuncia hasta
  abrirlo. El backend sigue mandando `razonamiento` en toda respuesta, pero el cliente sólo lo
  renderiza con `VITE_ASISTENTE_DEBUG=true` (opt-in explícito, sin fallback a modo desarrollo).
  `preguntaInterpretada` queda **visible** (RF-10), no dentro, y no depende del flag.
- **`metricas.categoria` no se muestra y sale del tipo TS**: es una etiqueta interna
  (`consulta_simple`, `cruce_de_tablas`…). De `cubre[]` no se muestra ni `nombre` —`schema.tabla`—
  ni `descripcion`: es el comentario de la tabla en PostgreSQL, el mismo texto que el backend le
  manda al modelo en el prompt, con nombres de tablas y advertencias para el modelo. De las áreas
  sólo se dice cuántas hay, en la línea del alcance; una descripción para el usuario es trabajo del
  backend.
- **El rol elige el wording y nada más.** El repo evita ramificar por rol a propósito —una lista de
  roles embebida falla ABIERTA, y por eso la autorización pregunta por el permiso— pero esa regla
  protege la autorización, no la copy. Acá el rol elige un texto de bienvenida: un rol desconocido
  cae al genérico, que no promete nada de más, así que el modo de falla es inocuo. El rol no toca el
  alcance, los permisos, la conexión ni los ejemplos, que se siguen derivando de los GRANT efectivos
  y de la matriz de permisos, en vivo.
- **El conteo de áreas no se pierde con la presentación.** Es la única señal honesta de amplitud que
  tiene la pantalla: sin él, «Preguntá por los pedidos de tu carrera» se leería como el techo de lo
  que el asistente sabe. Queda debajo, en secundario, junto al alcance.
- **Sólo acciones reales por mensaje.** Copiar usa el portapapeles del navegador; si no
  está disponible, el botón no se renderiza. En v3 la barra suma ampliar tabla, exportar y el
  voto, y la última pregunta suma «Editar y reenviar» — todas con backend o con datos ya en el
  cliente. No hay regenerar ni adjuntar.
- **Reintentar reusa la clave de idempotencia del intento, y sólo aparece en un turno que terminó
  en error.** Es el uso documentado de la clave y no factura dos veces al modelo cuando el backend
  ya terminó. Un 404 reinicia el hilo antes de reintentar. Se verificó en el backend
  (`IdempotenciaEnMemoria.cs`, `AsistenteController.cs`) que la caché se consulta **antes** del
  turno y se guarda **después**, sin registrar el turno en curso: un reintento con la misma clave
  mientras el original sigue corriendo ejecutaría el turno completo otra vez. Por eso «Reintentar»
  nunca se ofrece en vuelo ni sobre un turno que el usuario dejó de esperar.
- **La conversación sobrevive al cierre del modal.** El estado sube del panel al lanzador, que
  vive mientras viva la barra superior. Un clic afuera ya no destruye el hilo. Sigue muriendo al
  recargar (el backend no persiste; D3 del change anterior). Sin `localStorage`: guardar en el
  navegador filas con datos personales sin política de retención contradice §3.4 de la definición.
- **~~Página y modal son hilos independientes.~~** Superada en v3: queda un solo montaje, el
  modal, y el lanzador es el único dueño de la conversación.
- **El cliente nunca queda colgado.** Cada turno lleva `AbortSignal` y un timeout de 160 s (apenas
  sobre los 150 s del presupuesto del backend, para que el que corte sea el servidor con su mensaje
  de degradado). **«Dejar de esperar»** aborta el request y libera el campo; no promete cancelar el
  trabajo del servidor, que sigue hasta su presupuesto y cobra la cuota, y el copy lo dice. Se
  eligió ese nombre y no «Detener» ni «Cancelar» porque describe exactamente lo que pasa.
- **La tabla scrollea dentro de su propio marco.** Se sobreescribe el `overflow: hidden` del
  envoltorio de la librería con una clase propia (no se forkea ni se usa `!important`); cabecera
  pegajosa; `width: max-content` para que las columnas no se aplasten.
- **Las columnas sensibles se marcan**, con candado y texto para lector de pantalla. Se dice qué es
  personal, no por dónde viajó.
- **La página y el modal se ven igual**: se anulan el color y el tamaño que el `.body` del `Modal`
  impone; el panel tiene alto fijo en ambos montajes para que sólo scrollee el hilo.
- **Ancho de lectura 880 px y texto a 72ch; tabla a ancho completo.** La respuesta no es burbuja
  angosta porque trae tablas y menús (decisión de `c38ebc8`, se mantiene).
- **«Enviar» con etiqueta visible, no ícono solo.** Anti-patrón #6 de los principios; y Enter es
  un atajo, no la única vía (lector de pantalla, móvil). Se renombra desde «Preguntar» para no
  tener dos botones iguales en el DOM con el modal abierto; el lanzador sigue «Preguntar».
- **Foco**: al abrir, en el campo; al responder, al campo; al cerrar, al lanzador; Tab contenido
  con `inert` sobre `#root` mientras el modal está abierto. La región viva sigue siendo sólo la
  lista de mensajes; indicador y métricas quedan fuera. El `Modal` de la librería debería traer
  focus trap y retorno propios: queda como deuda técnica y el workaround se quita cuando lo traiga.
- **Tokens del tema, nada propio.** Se eliminan todos los fallbacks slate/indigo del CSS actual.
  Íconos SVG a mano en `app/shell/icons.tsx`, en la grilla del shell; sin `lucide-react`.
- **El vínculo al detalle sale del backend, y la ausencia de vínculo también.** Que una fila se
  muestre no significa que su pantalla esté abierta para quien pregunta: las filas las filtra la
  RLS del asistente y el detalle lo autoriza el módulo de designaciones, con reglas que **hoy
  divergen** (el ámbito departamental es una lista de códigos de rol allá y `roles.scope` acá; el
  permiso es un claim del token allá y la matriz en vivo acá). La interfaz no adivina: pinta
  enlace donde el backend dijo que hay, y texto donde no. Un enlace que termina en 403 es fake UI
  (invariante #7), y la dirección segura del error es no ofrecerlo — el dato se lee igual.
- **Seguir un vínculo cierra el modal y conserva la conversación.** Quedarse tapando la pantalla
  a la que se acaba de llegar no tendría sentido. El hilo sobrevive porque vive en el lanzador,
  que sigue montado en la barra mientras la aplicación navega por debajo; al reabrir, la
  conversación está donde estaba.
- **Móvil**: fuera del alcance de v3 (sólo escritorio). Sin página propia, en un teléfono el
  asistente queda inalcanzable hasta la épica de mobile (TD-024, ligado a TD-016).
- **Copy en voseo rioplatense**, coherente con el backend y la definición, aunque los principios
  generales pidan evitar el «vos» informal en mensajes del sistema: la superficie entera del
  asistente ya habla así y mezclar registros sería peor.

## Anti-patterns a evitar (específicos de esta feature)

- Etapas de progreso («Interpretando… consultando…») o barras de progreso simuladas. La espera
  pareja no es esto: no afirma nada sobre qué está pasando, sólo demora cuándo aparece una
  respuesta que ya está.
- Streaming aparente (texto que «se escribe solo» con un temporizador).
- Retener un error, un servicio degradado, o cualquier respuesta del lado del **servidor**: la
  espera pareja vive en el cliente justamente para que `latencia_ms` siga siendo cierto.
- Un «Dejar de esperar» que diga «cancelar» o insinúe que no se cobró la consulta.
- «Reintentar» sobre un turno en vuelo o que se dejó de esperar: el backend ejecutaría el turno dos
  veces con la misma clave.
- Persistir la conversación en `localStorage`/`sessionStorage`.
- Botones de regenerar, adjuntar, voz: no hay backend. (Pulgar arriba/abajo, historial y
  «Editar y reenviar» la última pregunta sí lo tienen desde `asistente-feedback-export-seguimiento`,
  `asistente-historial-conversaciones` y `asistente-rediseno-v3` — ver más abajo.) Versiones de una
  pregunta («N / M»): descartadas por decisión de producto.
- Mostrar `estado`, `metricas.categoria`, `cubre[].nombre`, `cubre[].descripcion` —el comentario
  escrito para el modelo—, códigos HTTP, nombres de excepciones.
- Contar filas faltantes («ves 3 de 124»).
- Región viva sobre el contenedor entero; métricas dentro del log.
- Ocultar acciones sólo detrás de hover: en v3 la barra de acciones se revela también con el foco
  del teclado y sus controles siempre están en el orden de tabulación.
- Spinner en el botón de envío (parpadea en respuestas deterministas).
- Burbuja angosta para la respuesta (rompe tablas).
- Colores o radios inventados fuera de `@ars-docendi/ui/theme.css`.

## Historial de conversaciones (asistente-historial-conversaciones)

> En v3 el cajón superpuesto y el botón «Historial» del encabezado se reemplazan por el rail
> de § Rediseño v3; siguen valiendo el agrupado por fecha, la búsqueda, reanudar, «Volver a
> consultar», los anuncios en la región viva y la pantalla de soporte.

Sección agregada por `asistente-historial-conversaciones`: ninguna sección anterior de este spec
cubría chrome de historial, así que va acá en vez de forzarla en el flujo principal, que sigue
siendo el de una conversación viva.

**Reutiliza el panel, no lo duplica.** «Historial» es un botón `ghost` con ícono en el ENCABEZADO
de cada montaje —junto a «Nueva conversación», en el título del modal y en las acciones de la
página de la ruta—, así que aparece en los dos lugares sin dos implementaciones (rediseño: vivía
antes dentro de `PanelAsistente`, en su propia fila). Al pulsarlo, un cajón se abre SUPERPUESTO al
hilo, no un segundo modal ni una caja que empuja la conversación hacia abajo: a ancho de
escritorio ocupa un ancho fijo (340px) desde la izquierda, con un fondo que cierra al clic sobre lo
que queda visible del hilo al lado; a ancho angosto ocupa todo el panel. El patrón —cajón lateral,
agrupado por fecha, título de una línea, acciones detrás de un «⋮»— es el que comparten ChatGPT,
Claude.ai, Gemini y Microsoft Copilot para esta misma lista.

- **Lista**: agrupada por fecha relativa de última actividad (Hoy / Ayer / Últimos 7 días /
  Anteriores; un grupo sin conversaciones no aparece), con encabezado de grupo pegajoso. Cada fila
  es una línea con el título recortado con «…» (el título completo queda en el atributo `title`,
  visible al pasar el mouse) — abrirla reanuda la conversación — y, si es la que está reanudada en
  el hilo actual, queda marcada (`aria-current`, acento de la aplicación). Las acciones de la fila
  —«Renombrar» (campo inline, Guardar/Cancelar) y «Borrar» (pide confirmación inline antes de
  llamar al endpoint — nunca borra al primer clic)— viven detrás de un menú «⋮» que aparece con el
  mouse, el teclado o ya abierto, y no siempre visible: en una lista de varias conversaciones son
  ruido hasta que se necesitan. «Borrar todas» se mudó al PIE del cajón, no a la cabecera: no es lo
  primero que se busca al abrir el propio historial, y competía con el buscador por la misma fila.
  Pide la misma confirmación inline y queda deshabilitado sin conversaciones.
- **Buscar**: campo `search` en la cabecera del cajón, con debounce (300 ms) contra
  `GET /historial?q=`; sin coincidencias, «Ninguna conversación coincide con esa búsqueda.»; sin
  conversaciones, «Todavía no tenés conversaciones guardadas.»
- **Cerrar**: una «×» en la cabecera del cajón (mismo lenguaje visual que la del `Modal`/`Drawer`
  de la librería), el fondo (a ancho de escritorio) y Escape. Los tres devuelven el foco al botón
  «Historial» del encabezado.
- **Reanudar**: abrir una conversación pide `POST /historial/{id}/reanudar`, cierra el panel de
  historial y pinta sus turnos pasados en el MISMO hilo que ya se usa para la conversación en
  vivo — no hay una vista separada de «modo lectura». Cada turno restaurado muestra la pregunta,
  su desenlace («Esta pregunta fue respondida.», «…no se pudo responder.», «…necesitaba una
  aclaración.», «El servicio estaba degradado…») y, con `asistente.ver_consulta`, la consulta
  guardada — nunca el texto redactado, porque nunca se persiste (ver capability
  `asistente-historial-conversaciones`, decisión D2/D4 del design de ese change). El foco pasa al
  campo de la pregunta, listo para un seguimiento: es el mismo efecto que ya mueve el foco al
  campo tras cualquier turno, sin código nuevo para esto.
- **«Volver a consultar»**: sólo en un turno restaurado que terminó respondido. Re-ejecuta la SQL
  guardada bajo el alcance ACTUAL del actor (nunca llama al modelo, nunca escribe historial nuevo)
  y pinta la tabla de siempre (`TablaDeResultado`) con «Consulta actualizada.» arriba. Si la
  consulta ya no corre (privilegios que se achicaron, esquema que cambió), un `InlineAlert info`
  con un texto no técnico reemplaza la tabla — nunca un error crudo, nunca una caída visible.

**Anuncios: la MISMA región viva, ninguna nueva.** Renombrar, borrar (uno o todos), reanudar y
«Volver a consultar» no tienen turno propio del que colgar su confirmación, así que
`Conversacion.tsx` acepta un `anuncio` opcional que se pinta como un último ítem de su propia
`<ul role="log" aria-live="polite">` — la región que ya existe, nunca una segunda. Borrar y
renombrar además devuelven el foco al contenedor del cajón (con `tabIndex={-1}`) en vez de
perderlo en `<body>`, porque la fila o el formulario que tenía el foco se desmonta con la acción.

**Pantalla de soporte, en su propia ruta.** `/asistente/soporte-historial` (permiso
`asistente.leer_historial_ajeno`, sembrado a NINGÚN rol) reutiliza `RequirePermission` —mismo
mecanismo que ya protege `/designaciones/periodos`—, así que sin el permiso no hay ítem de
navegación y la ruta ni resuelve. La pantalla: un buscador de personas (reusa el endpoint real de
administración de usuarios, no un buscador nuevo) para elegir a quién, un campo de razón
obligatorio, y recién con los dos la lista de sus conversaciones y, al abrir una, su detalle. El
detalle muestra pregunta, consulta (siempre, sin el gate de `asistente.ver_consulta` — el permiso
de soporte ya es el de diagnóstico), desenlace y momento — **nunca** una fila de resultado ni un
botón de «Volver a consultar»: esa acción no existe para el historial ajeno, ni por soporte ni por
nadie. Al sujeto nunca se le muestra que alguien miró su historial: no hay pantalla ni endpoint
para eso, por decisión final del cliente.

**Accesibilidad.** Todo el cajón es alcanzable por Tab en orden lógico (cerrar → buscar → abrir →
«⋮» de cada fila → borrar todas, al pie) y cada acción activa con Enter o Espacio igual que con un
clic — incluidas «Renombrar» y «Borrar» detrás del menú, que sigue el mismo patrón de menú `⋮` ya
establecido en el proyecto (`role="menu"`/`role="menuitem"`, `aria-expanded` en el disparador). Las
dos confirmaciones de borrado son controles de teclado comunes, no un modal aparte. Escape cierra
el cajón y devuelve el foco al botón «Historial» del encabezado. El botón «Volver a consultar» es
un botón como cualquier otro: alcanzable, operable, y su resultado se anuncia por la región viva ya
descripta.

## Administración de uso (asistente-administracion-de-uso)

Sección agregada por `asistente-administracion-de-uso`: el indicador de cupo y el banner de
mantenimiento tocan el chrome del flujo principal (§ Layout / IA, § Estados a diseñar); el panel
de administración es una pantalla nueva, en su propia ruta, mismo patrón que la de soporte de
historial arriba.

**Indicador de cupo restante, en la tira de estado, fuera de la región viva.** Junto al alcance y
la presentación (§ Layout / IA), un texto chico y no interactivo: «Te quedan 7 consultas hoy.» Sale
de `capacidades.cupo` al cargar la pantalla y se actualiza, tras cada turno, con
`respuesta.cupoRestante` — **nunca** con un segundo `GET /capacidades`: son dos actualizaciones
independientes de la interfaz (la tira de estado y la región viva del turno), y la del cupo no
mueve el foco ni se anuncia dentro del anuncio del turno. Con el cupo desactivado (`restante` en el
valor centinela de "sin tope"), el indicador no se muestra — un número sin techo no es información,
es ruido. Bloqueado, el mismo lugar dice la causa en TEXTO, nunca sólo con color: «Alcanzaste tu
límite de hoy.», «El asistente alcanzó el límite de uso de la organización.», o el motivo de
mantenimiento (ver abajo) — nunca los tres juntos, porque `capacidades.cupo.motivo` es uno solo.

**Banner de mantenimiento, visible y no descartable, con el campo deshabilitado.** Cuando
`capacidades.mantenimiento.activo` es verdadero, un `InlineAlert warning` fijo arriba del panel —
mismo lenguaje visual que «Servicio degradado» en § Estados a diseñar, nunca rojo — con la razón
que el admin escribió: «El asistente está en mantenimiento: {razón}.» El campo de pregunta queda
deshabilitado mientras el banner esté visible, con el mismo estado que «En vuelo» le da al botón
«Enviar». Un actor con `asistente.administrar` sigue viendo el banner (la verdad global no cambia
para nadie) pero su campo NO se deshabilita — puede seguir consultando para verificar la
recuperación, que es exactamente lo que el bypass del backend habilita (design.md D8).

**Pantalla de administración, en su propia ruta.** `/asistente/administracion` (permiso
`asistente.administrar`, sembrado a `sys_admin`) reutiliza `RequirePermission` — mismo mecanismo
que ya protege `/asistente/soporte-historial` y `/designaciones/periodos` — así que sin el permiso
no hay ítem de navegación y la ruta ni resuelve. Tres bloques:

- **Panel de uso**: selector de período (día/semana/mes, o rango explícito) arriba; tres tablas —
  por usuario (con nombre, resuelto por el backend), por rol, y una fila organizacional destacada —
  cada una con turnos, desglose por resultado, llamadas al modelo, tokens y latencia. El costo
  estimado lleva SIEMPRE la etiqueta «(estimado)» junto al número — nunca un número solo, para no
  leerse como la factura real — y una fila cuyo proveedor no tiene precio cargado muestra «sin
  precio» en el lugar del costo, en un estilo visualmente distinto (nunca «$0», que se leería como
  gratis). Un período sin datos muestra «No hay uso registrado en este período.», sin romper el
  layout de la tabla.
- **Presupuestos**: un control por rol de sistema (cupo diario, `0` = sin tope) y un campo con el id
  del actor puntual para fijar su override, más el tope organizacional mensual. **NO reusa el
  buscador de personas de la pantalla de soporte de historial** — a diferencia de la lectura de
  soporte, ese buscador exige `usuarios.ver` (`GET /api/administracion/usuarios`), y design.md D12
  de esta misma change existe justamente para que un administrador con SÓLO `asistente.administrar`
  no necesite ningún otro permiso; sumarle el buscador reintroduciría la dependencia que D12 evita,
  ahora en la mitad de escritura del panel en lugar de la de lectura. Guardar un valor que BAJA un
  cupo hoy activo pide confirmación inline antes de aplicar — mismo patrón que «Borrar» en el cajón
  de historial —; subirlo o desactivarlo (`0`) no la pide: sólo lo que puede cortarle a alguien una
  consulta en curso o próxima es lo que amerita la pausa.
- **Modo mantenimiento**: un interruptor con un campo de razón que se vuelve obligatorio recién al
  querer PRENDERLO — el backend rechaza la activación sin razón (tarea 6.2), y el campo replica esa
  regla en el cliente para no depender de un 400 para avisar. Apagarlo no pide razón. Cada cambio se
  confirma por la misma región viva que ya usa el resto del panel.

**Accesibilidad.** Selector de período, controles de presupuesto y el interruptor de mantenimiento
son alcanzables por Tab en orden lógico y operables con Enter/Espacio, mismo patrón que el resto del
proyecto. Un presupuesto guardado y un toggle de mantenimiento completado se anuncian por la región
viva sin mover el foco. El banner de mantenimiento y el indicador de cupo son legibles por lectores
de pantalla sin depender de `hover` ni de `title` — nunca la única forma de enterarse de una causa
de bloqueo.

## Rediseño v3 — modal con historial integrado (asistente-rediseno-v3)

Sección agregada por `asistente-rediseno-v3` (épica ARS-140, decisiones del 2026-09-26).
Referencia visual: Claude Design «Asistente v3 · menos ruido» (`Asistente v3.dc.html`,
adjunto en ARS-140). Alcance: **sólo escritorio**; el diseño angosto/móvil queda fuera (TD-016,
TD-024). Donde el mock y este spec difieren, gana este spec (ver «Desvíos del mock» abajo).

### Layout

```
┌ Modal 1100 × 728 (máx.) ───────────────────────────────────────────────────────────┐
│ Rail 268 px (60 px colapsado)  │ Encabezado 56 px: «Título de la conversación» (?) × │
│ [＋ Nueva conversación] [⇤]     ├─────────────────────────────────────────────────────┤
│ [🔍 Buscar…]                    │ Hilo (scrollea solo, columna de 720 px)              │
│ HOY                             │   • Bienvenida (sin turnos)                          │
│ ▌Conversación activa        ⋮   │   • Turno: pregunta (derecha) + herramientas         │
│  Otra conversación          ⋮   │            respuesta, tabla, barra de acciones       │
│ ANTERIORES …                    │                                                      │
│ ─────────────                   ├─────────────────────────────────────────────────────┤
│ ▸ ARCHIVADAS             2      │ ✦ [Preguntá algo · @ materia · # docente ] [Enviar] │
│ [Borrar todas]                  │ Franja: cupo restante / bloqueo …… métricas          │
│ ┌ Conversación eliminada  Deshacer ┐                                                  │
└───────────────────────────────────────────────────────────────────────────────────────┘
```

- **Grilla de dos columnas** `268px minmax(0,1fr)` ↔ `60px minmax(0,1fr)`, transición de
  `grid-template-columns` con `--motion-base` y `--ease-standard` (200 ms,
  `cubic-bezier(0.2,0,0,1)`, exactamente los del mock); sin transición con movimiento reducido.
  El modal mide `min(1100px, 100vw − 48px)` × `min(728px, alto útil − 48px)`, con el mismo
  cálculo de zoom que hoy.
- **Rail expandido**: «＋ Nueva conversación» (botón con borde `--color-border-strong`) y el
  botón de colapsar; debajo, el buscador (se mantiene aunque el mock no lo dibuje: las archivadas
  tienen que seguir encontrándose); la lista agrupada por fecha relativa (Hoy / Ayer / Últimos 7
  días / Anteriores; el mock muestra sólo dos grupos por sus datos de ejemplo); la sección
  «Archivadas N» al pie, colapsada por defecto y oculta sin archivadas; «Borrar todas»; y el
  aviso de deshacer. **Rail colapsado**: sólo «Expandir conversaciones», «Nueva conversación» e
  «Historial» (que expande). El estado se recuerda por usuario en el navegador (única
  preferencia guardada; nunca turnos).
- **Fila**: una línea de 36 px, título recortado con «…» y completo en `title`; «⋮» a la derecha,
  siempre visible en tono tenue, más oscuro con fondo al hover o al foco (nunca oculto en reposo:
  el mock lo dibuja en cada fila). La activa: fondo `--color-accent-subtle`, texto
  `--color-accent-pressed`, peso 500, `aria-current`. Doble clic o «Renombrar» → campo inline con
  borde de foco en acento; Enter o salir del campo guarda, Escape cancela, vacío conserva el título.
- **Menú «⋮»** (`MenuAcciones` compartido): «Renombrar» / «Archivar» / «Eliminar» en filas
  activas; «Desarchivar» / «Eliminar» en archivadas. «Eliminar» en `--color-text-danger`.
- **Encabezado** 56 px: título de la conversación activa (o «Asistente» en la bienvenida),
  «?» (`AyudaDelAsistente`: presentación, alcance, cantidad de áreas y límites) y «×». El
  nombre accesible del diálogo sigue siendo «Asistente».
- **Hilo**: columna centrada de 720 px, 44 px entre turnos. Pregunta a la derecha en burbuja
  neutra (`--color-bg-canvas`, texto primario, radio `--radius-sm`, ≤ 80 %); debajo, sus
  herramientas: «Copiar pregunta» siempre y «Editar y reenviar» sólo en la última.
- **Respuesta**: texto a 16 px/1,6 y `68ch`; «Entendí: …» si hubo reinterpretación; tabla con
  borde `--color-border-default`, `max-height` 260 px con scroll propio y cabecera pegajosa sobre
  `--color-bg-canvas`; aviso de truncado; «Ver la consulta» (con permiso) y «Cómo lo interpreté»
  (sólo modo debug); **barra de acciones** de íconos de 28 px: «Copiar respuesta», «Ampliar
  tabla», «Exportar a CSV» | «Sirvió», «No sirvió».
- **Composer**: marco con borde `--color-border-default` que pasa a acento con foco; destello en
  `--color-accent`; placeholder «Preguntá algo · @ materia · # docente»; fila de chips de
  menciones; el lugar del botón muestra «Enviar» (acento; deshabilitado vacío o en vuelo) o
  «Dejar de esperar» (pasado el umbral). Debajo, la franja con el cupo y las métricas.

### Componentes y estados

| Elemento                  | Comportamiento                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Bienvenida                | Destello en cuadro `--color-accent-subtle`, «¿Qué querés saber del sistema?» y los ejemplos del catálogo como tarjetas en grilla de 2 columnas con flecha; son las **únicas** sugerencias clicables del asistente.                                                                                                                                                                                                                                                                                                            |
| Consultando…              | Tres puntos que laten (`--color-accent`, apagados con movimiento reducido) + «Consultando…» en el lugar de la respuesta, pasados 400 ms. Visual, `aria-hidden`; el anuncio sigue siendo el `role="status"` de siempre, fuera del log. Sin etapas.                                                                                                                                                                                                                                                                             |
| Dejaste de esperar        | «Dejaste de esperar la respuesta. La consulta ya salió y cuenta para tu cupo.» en texto secundario, sin alerta ni «Reintentar».                                                                                                                                                                                                                                                                                                                                                                                               |
| Error                     | Caja con borde `--color-border-danger`, fondo `--color-status-danger-bg`, texto `--color-status-danger-fg`: «No se pudo consultar» + mensaje + «Reintentar».                                                                                                                                                                                                                                                                                                                                                                  |
| Degradado / aclaración    | `InlineAlert` warning / info como hoy; rechazos sin chips (y los rechazos por motivo de ARS-139, cuando lleguen, tampoco).                                                                                                                                                                                                                                                                                                                                                                                                    |
| Mantenimiento / cupo      | Banner warning arriba del hilo; cupo restante y texto de bloqueo en la franja bajo el composer, fuera de la región viva.                                                                                                                                                                                                                                                                                                                                                                                                      |
| Barra de acciones         | Visible en el último turno y en los votados; en los demás aparece con hover **o foco dentro del turno**; siempre en el orden de Tab. Íconos con tooltip y nombre accesible. «Copiado»: tilde en acento 2 s.                                                                                                                                                                                                                                                                                                                   |
| Orden de la tabla         | Encabezado como botón: «⇅» tenue al hover/foco, «↑»/«↓» en acento en la columna activa; `aria-sort` sólo en ésa. Número, fecha ISO o texto en español; vacíos al final; siempre sobre el valor mostrado.                                                                                                                                                                                                                                                                                                                      |
| Tabla ampliada            | Capa sobre todo el modal: eyebrow «Tabla ampliada» (mono, mayúsculas, `--color-text-tertiary`) + la pregunta como título; «Copiar tabla», «Exportar a CSV», «Contraer». Esc contrae sin cerrar el modal; el foco vuelve a «Ampliar tabla».                                                                                                                                                                                                                                                                                    |
| 👎 «¿Qué falló? Opcional» | Panel `--color-bg-surface` con pastillas de elección única (`aria-pressed`): «Datos incorrectos», «No entendió la pregunta», «Faltan datos», «Otro»; «Omitir» / «Enviar». Después: «Gracias. Tu comentario ayuda a mejorar el asistente.»                                                                                                                                                                                                                                                                                     |
| Editar y reenviar         | Sólo la última pregunta: textarea inline con borde de acento, «Cancelar» / «Enviar»; la respuesta queda al 40 % de opacidad mientras se edita; Escape cancela y devuelve el foco. Reenviar **reemplaza** pregunta y respuesta; sin «N / M».                                                                                                                                                                                                                                                                                   |
| Menciones                 | «@» materias, «#» docentes. Menos de 2 letras: aviso «Escribí al menos 2 letras para buscar materias.». Popover con borde fuerte: grupo, ícono, nombre, carrera, código (materias) o cargo (docentes), «Hay más coincidencias. Seguí escribiendo para acotar.», vacío «Sin materias que coincidan en las carreras a las que tenés acceso.», pie con candado «Solo aparecen materias y docentes de las carreras a las que tu perfil tiene acceso.» y «Enter elige · Esc cierra». Lo elegido queda como chip y viaja con su id. |
| Aviso de deshacer         | Fondo `--color-bg-inverse`, texto `--color-text-on-inverse`, al pie del rail: «Conversación archivada» / «Conversación restaurada» / «Conversación eliminada» / «Conversaciones eliminadas» + «Deshacer», **10 s**. Uno a la vez; sigue visible con el rail colapsado (se superpone al hilo).                                                                                                                                                                                                                                 |
| Borrar todas              | Pide confirmación inline: «¿Borrar TODAS tus conversaciones, incluidas las archivadas? Vas a poder deshacerlo durante 10 segundos.» y después muestra el aviso.                                                                                                                                                                                                                                                                                                                                                               |

### Tokens (del mock a `@ars-docendi/ui/theme.css`)

Los `oklch` del mock son exactamente la paleta de la librería, así que el mapeo es 1:1. En el
CSS de la feature **sólo** se usan los semánticos (ningún `oklch(` ni hex fuera de comentarios).

| Mock                                                 | Uso en el mock                                       | Token                                                                                                    |
| ---------------------------------------------------- | ---------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| `oklch(25% 0.005 75)`                                | texto, borde del modal                               | `--color-text-primary`, `--color-border-ink`                                                             |
| `oklch(38% 0.006 75)`                                | texto secundario, borde de botones                   | `--color-text-secondary`, `--color-border-strong`                                                        |
| `oklch(52% 0.007 75)`                                | placeholder, grupos, íconos                          | `--color-text-tertiary`                                                                                  |
| `oklch(68% 0.007 75)`                                | «⋮» y «⇅» en reposo                                  | `--color-text-disabled`                                                                                  |
| `oklch(83% 0.007 75)`                                | borde de tabla, pastillas, composer                  | `--color-border-default`                                                                                 |
| `oklch(91% 0.007 75)`                                | divisores del rail, encabezado y filas               | `--color-border-subtle`                                                                                  |
| `oklch(95% 0.006 75)`                                | hover, burbuja del usuario, cabecera de tabla        | `--color-bg-canvas`                                                                                      |
| `oklch(98% 0.004 75)`                                | fondo del rail, panel 👎, cabecera de tabla ampliada | `--color-bg-surface`                                                                                     |
| `#fff`                                               | modal, tarjetas, campos                              | `--color-bg-raised`                                                                                      |
| `oklch(14% 0.005 75)` / `98%`                        | aviso de deshacer (fondo / texto)                    | `--color-bg-inverse` / `--color-text-on-inverse`                                                         |
| `oklch(57% 0.115 165)`                               | «Enviar», foco, orden activo, destello               | `--color-accent`, `--color-border-focus`                                                                 |
| `oklch(48% 0.1 165)`                                 | hover de acento, enlaces                             | `--color-accent-hover`, `--color-text-link`                                                              |
| `oklch(38% 0.08 165)`                                | texto de la fila activa, 👍 activo                   | `--color-accent-pressed`                                                                                 |
| `oklch(94% 0.03 165)`                                | fila activa, pastilla elegida, opción activa         | `--color-accent-subtle`                                                                                  |
| `oklch(86% 0.06 165)`                                | hover del «⋮» en la fila activa                      | `color-mix(in srgb, var(--color-accent) 18%, transparent)` (patrón ya usado en la feature)               |
| `oklch(74% 0.09 165)`                                | «Deshacer» sobre fondo oscuro                        | `--color-text-on-inverse` en semibold (ver desvíos)                                                      |
| `oklch(55% 0.18 25)` / `94% 0.04 25` / `40% 0.14 25` | error y «Eliminar»                                   | `--color-border-danger` / `--color-status-danger-bg` / `--color-status-danger-fg`, `--color-text-danger` |
| 4 px / 2 px / 9999 px                                | radios                                               | `--radius-sm` / `--radius-xs` / `--radius-pill`                                                          |
| 200 ms `cubic-bezier(0.2,0,0,1)`; 120–150 ms         | rail; opacidades y chevron                           | `--motion-base` + `--ease-standard`; `--motion-fast`                                                     |
| Inter / JetBrains Mono                               | texto / eyebrows, códigos, números de tabla          | `--font-sans` / `--font-mono`                                                                            |

### Interacciones y foco

- Colapsar/expandir: Enter o Espacio; el foco queda en el conmutador visible del nuevo estado
  (`aria-expanded`).
- Archivar/eliminar desde «⋮»: la fila desaparece, el anuncio sale por la región viva existente
  («… Podés deshacerlo durante 10 segundos.») y el foco pasa a «Deshacer»; al deshacer, a la fila
  restaurada; si vence con el foco adentro, a la lista. Archivar o eliminar la conversación
  activa vuelve a la bienvenida; «Deshacer» la reanuda.
- Esc cierra, en este orden y sin cerrar el modal: el popover de menciones, el menú «⋮», la
  edición inline, la tabla ampliada. Con nada de eso abierto, Esc cierra el modal como hoy.
- Vínculos de trámite en celdas: se mantienen (también en la tabla ampliada) y siguen cerrando
  el modal al navegar.
- `/asistente` → home (`/portal`) con el modal abierto; sin acceso, sólo la home.

### Desvíos del mock (decididos en `asistente-rediseno-v3`, pendientes de confirmación del PO)

- Sin navegación de versiones «N / M» ni la nota de versiones al editar (descartado por producto).
- El aviso dura **10 s** (el mock usa 5 s).
- El panel 👎 **no tiene** el campo «Contanos qué esperabas ver…» y las pastillas son de elección
  única: el conjunto de motivos es cerrado y un texto libre junto a una fila anónima es el canal
  de re-identificación que TD-012 cierra. El botón dice «Enviar», no «Enviar comentario».
- Las materias del popover van una fila por materia y carrera (no una fila con varias carreras
  como etiquetas): cada fila corresponde a un id exacto.
- Se mantienen aunque el mock no los dibuja: buscador del rail, «Borrar todas», franja con cupo y
  métricas, banner de mantenimiento, «Ver la consulta», «Cómo lo interpreté» (debug) y los cuatro
  grupos de fecha.
- «Deshacer» usa `--color-text-on-inverse` en semibold en lugar del acento claro del mock: el
  acento no alcanza 4,5:1 sobre `--color-bg-inverse` en ninguno de los dos temas.

## Referencias

- [`docs/product/design-principles.md`](../design-principles.md)
- Spec funcional: [`openspec/changes/asistente-rediseno-conversacion/specs/asistente-conversacion/spec.md`](../../../openspec/changes/asistente-rediseno-conversacion/specs/asistente-conversacion/spec.md)
- Rediseño v3: [`openspec/changes/asistente-rediseno-v3/`](../../../openspec/changes/asistente-rediseno-v3/) y épica ARS-140
- [Definición del asistente](./asistente-conversacional-definicion.md) §3.1, §3.2, §4.6, §4.7
- Change previo: `openspec/changes/asistente-frontend/` (D1-D8)
- Ticket ARS-79 (razonamiento / RF-11)
- Commit `c38ebc8` (modal, burbujas, campo de una línea)

## Open questions de diseño

- Los umbrales (contador a 1 800 caracteres, anclaje a 24 px del fondo) se eligieron por
  razonamiento, no por medición; ajustar con uso real.
- Mockup: cuando el equipo elija la herramienta de diseño, llevar este spec a un frame y linkearlo
  desde `exports/asistente-conversacional/`.
