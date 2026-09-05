---
status: review
owner: "Equipo Ars Docendi"
feature: "openspec/changes/asistente-rediseno-conversacion/specs/asistente-conversacion/spec.md"
last_updated: 2026-09-04
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
   Alternativa: navega a `/asistente` y ve la misma vista a página completa.
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
   opciones de aclaración o las sugerencias; y al pie, colapsados, «Cómo lo interpreté»
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
- **Página `/asistente`**: `PageHeader` («Asistente» + meta) con «Nueva conversación» en
  `actions`; el panel centrado a 880 px y con alto fijo para que el hilo scrollee solo.
- **Tarjeta de respuesta**: fondo `--color-bg-sunken`, radio `--radius-sm`, texto a `72ch`; la
  tabla ocupa todo el ancho de la tarjeta, con cabecera pegajosa y `max-height: 50vh`.
- **Burbuja del usuario**: `--color-accent` / `--color-text-on-accent`, alineada a la derecha.
- **Opciones de aclaración**: bloque con barra de acento a la izquierda, botones `secondary`.
- **Sugerencias**: chips pastilla `ghost`, bajo el texto «Probá con alguna de estas:».
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
| No contestable      | Texto del backend + sugerencias como chips                                                                                                                                                   | `estado = no_contestable`                                                      |
| Necesita aclaración | `InlineAlert info` «Necesito que precises algo» + opciones que continúan el turno                                                                                                            | `estado = necesita_aclaracion`                                                 |
| Servicio degradado  | `InlineAlert warning` «El asistente no está disponible ahora» + texto del backend (cupo, proveedor); nunca rojo                                                                              | `estado = servicio_degradado`                                                  |
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

- **`razonamiento` va dentro del mensaje, colapsado** (cierra ARS-79 variante 1, RF-11). Un
  `<details>` nativo con resumen «Cómo lo interpreté»: parte de la respuesta, en la región viva, y
  no se anuncia hasta abrirlo. `preguntaInterpretada` queda **visible** (RF-10), no dentro.
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
- **Sólo acciones reales por mensaje: copiar.** Copiar usa el portapapeles del navegador; si no
  está disponible, el botón no se renderiza. No hay regenerar, feedback, editar ni adjuntar.
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
- **Página y modal son hilos independientes.** Dos montajes, dos estados, como hoy. Unificarlos
  es levantar el estado a un contexto del `AppLayout` y una decisión de producto que no se toma
  hasta tener feedback de uso real.
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
- **Móvil a pantalla completa** desde 640 px hacia abajo; Enter hace salto en puntero grueso.
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
- Botones de regenerar, pulgar arriba/abajo, editar mensaje, adjuntar, voz, historial de
  conversaciones: no hay backend.
- Mostrar `estado`, `metricas.categoria`, `cubre[].nombre`, `cubre[].descripcion` —el comentario
  escrito para el modelo—, códigos HTTP, nombres de excepciones.
- Contar filas faltantes («ves 3 de 124»).
- Región viva sobre el contenedor entero; métricas dentro del log.
- Ocultar acciones sólo detrás de hover.
- Spinner en el botón de envío (parpadea en respuestas deterministas).
- Burbuja angosta para la respuesta (rompe tablas).
- Colores o radios inventados fuera de `@ars-docendi/ui/theme.css`.

## Referencias

- [`docs/product/design-principles.md`](../design-principles.md)
- Spec funcional: [`openspec/changes/asistente-rediseno-conversacion/specs/asistente-conversacion/spec.md`](../../../openspec/changes/asistente-rediseno-conversacion/specs/asistente-conversacion/spec.md)
- [Definición del asistente](./asistente-conversacional-definicion.md) §3.1, §3.2, §4.6, §4.7
- Change previo: `openspec/changes/asistente-frontend/` (D1-D8)
- Ticket ARS-79 (razonamiento / RF-11)
- Commit `c38ebc8` (modal, burbujas, campo de una línea)

## Open questions de diseño

- Los umbrales (contador a 1 800 caracteres, anclaje a 24 px del fondo) se eligieron por
  razonamiento, no por medición; ajustar con uso real.
- Mockup: cuando el equipo elija la herramienta de diseño, llevar este spec a un frame y linkearlo
  desde `exports/asistente-conversacional/`.
