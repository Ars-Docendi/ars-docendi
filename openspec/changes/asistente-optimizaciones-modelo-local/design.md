## Context

Continúa a `asistente-proveedor-local`. La investigación y las mediciones del prefijo están en `docs/architecture/modelo-local.md`.

Medido sobre el fixture de evaluación, el prefijo de la generación tiene **29.524 caracteres**, unos 12k tokens:

| Parte                    | Caracteres |
| ------------------------ | ---------- |
| Comentarios de columna   | 12.456     |
| Descripciones de tabla   | 6.041      |
| Instrucciones            | 3.767      |
| Lista de claves foráneas | 2.029      |
| Vocabulario cerrado      | ~400       |
| Nombres y tipos          | el resto   |

Los comentarios y las descripciones **son reglas del dominio**: «vigente_hasta NULO significa vigencia abierta», «nunca comparar contra el reloj». Recortarlos ahorraría tokens a costa de exactitud.

## Goals / Non-Goals

**Goals**

- Menos tokens de prefijo, menos llamadas por turno y menos espera percibida con un modelo local.
- Que entren dos turnos en vuelo en una RTX 3070 de 8 GB.
- Cero cambios con los defaults: el prompt de Claude y los cassettes quedan intactos.

**Non-Goals**

- **Podar el esquema por dominio.** El portal es el 24% del esquema, pero decidir con un léxico cuándo una pregunta lo necesita convierte un acierto de vocabulario en una abstención injusta, y la literatura es mixta (nl2sql-onprem-bench). Necesita el evaluador.
- **Self-consistency.** +0,13 puntos con p95 ×5: no.

## Decisions

### D1 — Todo es opt-in

Cada optimización es una opción de `OpcionesAsistente` que arranca apagada. Hay dos razones:

1. El prompt de Claude está medido. Los 109 cassettes, la línea de base del evaluador y `PrefijoDeLosCassettesTests` fijan su forma, y cambiarlo exige una regrabación financiada.
2. Para un modelo local cada una es una apuesta que el evaluador tiene que confirmar. Con opciones, comparar es cambiar una variable.

Los perfiles `compose.asistente-local.yml` (5070) y `compose.asistente-local-3070.yml` las prenden.

### D2 — Esquema compacto, sin pérdida de significado (`EsquemaCompacto`)

`RenderizadorDeEsquema` gana una variante con el mismo contenido en otra forma:

- `col tipo?` en lugar de `col (tipo, admite nulo)`, con tipos abreviados (`timestamptz`, `int`, `bool`, `varchar`, `float8`);
- la clave foránea en línea, `persona_id uuid → identity.personas.id`, en lugar de la sección «Cómo se relacionan», que repetía cada nombre de columna;
- sin el comentario de las columnas `id` que sólo dice «Identificador de …».

Una línea de leyenda explica el formato. Comentarios y descripciones se conservan enteros. Medido: **−11%** (29.524 → 26.276 caracteres).

### D3 — Ejemplos en el prefijo (`EjemplosEnElPrefijo`)

Los ejemplos verificados van **todos** al final del prefijo, ordenados como en el catálogo. Hoy son 20, unos 8 KB. El mensaje del turno deja de llevarlos.

- **Con caché de prefijo**, su costo marginal es casi nulo y estabiliza el prompt: el mensaje queda en fecha, consultas anteriores y pregunta.
- **Sin caché** son ~2,5k tokens más por llamada. Por eso el perfil de la 3070, donde la VRAM manda, lo deja apagado.

Lo arma `GeneradorDeSql` sobre el prefijo del esquema y lo memoriza por variante.

### D4 — Reintento con contexto y reparación

Las dos usan una regeneración con el **mismo prefijo** (la caché acierta) y el mensaje original más un bloque «Intento anterior»: la SQL y qué pasó.

- **`ReintentoConContexto`.** El reintento por consulta vacía dice «se ejecutó sin error y no devolvió filas» en vez de repetir el prompt idéntico. Con temperatura 0, el prompt idéntico devolvía la misma SQL.
- **`RepararConsultaFallida`.** Si PostgreSQL rechaza la consulta, hay **una** ronda con el error. Se excluyen:
  - `42501` (privilegio): es una abstención correcta, no un error de SQL;
  - `57014` (timeout de sentencia): una corrección no la abarata de forma confiable.

  Si la segunda también falla, el turno sigue por el camino de hoy.

- **El error se sanea.** Viaja el SQLSTATE y el mensaje y la pista del motor, pero cada literal entre comillas que **no** aparece en la SQL generada se reemplaza por `…`.

  Un literal que está en la SQL lo escribió el modelo; uno que no está puede venir de una fila. Por ejemplo, un cast que falla sobre un valor de una columna nombra ese valor. Así ningún dato de una fila llega al proveedor por fuera del enmascarador.

Las dos cuentan contra el techo de llamadas del turno. El peor caso sigue siendo 4: generación, reparación o reintento, regeneración y redacción.

### D5 — Respuestas sin modelo para resultados triviales (`RedaccionConPlantillas`)

Si el resultado tiene **una columna y entre 1 y 5 filas**, el actor ve todo, no hay recorte y no hay cobertura autodeclarada, el redactor responde con plantilla y no llama al modelo:

- una fila: «El resultado es X.»
- varias: «Encontré N resultados: A, B y C.»

Usa los valores **ya enmascarados**, igual que el modelo. Fuera de esas condiciones, las reglas de abstención de la redacción (no afirmar totales, encuadrar en el alcance) necesitan prosa, y sigue el modelo.

### D6 — Caché de consultas generadas (`VigenciaDeCacheDeConsultasMinutos`, 0 = apagada)

- **Clave:** pregunta normalizada (minúsculas, espacios colapsados, sin signos de pregunta de los extremos), variante de rol (básico/PII) y fecha de referencia.
- **Sólo turnos sin contexto:** sin consultas anteriores, sin preguntas anteriores y sin menciones.
- **Se guarda la generación**, nunca filas, y sólo después de que validó y trajo filas.
- **Un acierto se valida y se ejecuta de nuevo** bajo el alcance del actor que pregunta. RLS y GRANT deciden igual que siempre, y la consulta no lleva filtros de alcance (las instrucciones lo prohíben).
- **Tope de 500 entradas** con desalojo de la más vieja. Es por proceso.

### D7 — Reescritura dentro de la generación (`ReescrituraEnLaGeneracion`)

Con la opción prendida, el reescritor no se llama. La generación recibe las preguntas anteriores del segmento vigente y la instrucción de devolver `pregunta_interpretada`. Es clave opcional, primera en el esquema de salida, así el modelo resuelve la anáfora antes de escribir la SQL. El carril la usa como «así lo interpreté».

**Costo:** el detector de ambigüedad y el enrutador en sombra ven la pregunta cruda en los seguimientos. La literatura (MDPI 2025) mide mejor calidad multi-turno concatenando el historial que reescribiendo aparte, y se ahorra una llamada por seguimiento.

### D8 — Telemetría del servidor local

`GET /api/asistente/administracion/servidor-local` (`asistente.administrar`) devuelve:

- **`configurado`:** si el proveedor es `local`.
- **`alcanzable`:** si `/metrics` respondió en menos de 3 s.
- **Métricas del servidor:**
  - de vLLM: `vllm:num_requests_running`, `vllm:num_requests_waiting`, `vllm:kv_cache_usage_perc` y el cociente `prefix_cache_hits/queries`;
  - de `llama-server`: `llamacpp:requests_processing`, `llamacpp:requests_deferred` y `llamacpp:kv_cache_usage_ratio`.

  Una métrica que el servidor no expone queda en `null`, nunca en cero.

- **Estado de la compuerta:** capacidad, en curso y en espera.

El panel «Uso del asistente» suma una tarjeta que sólo aparece con proveedor local y se refresca cada 15 s.

### D9 — Streaming de la redacción (`StreamingDeRedaccion`)

- **Puerto.** `SolicitudAlModelo` suma `AlRecibirTexto` (`Func<string, CancellationToken, Task>?`), opcional.
  - El adaptador local, si viene, pide `stream: true` y lo invoca con cada fragmento. Los fragmentos de un bloque `<think>` se descartan.
  - El de Anthropic lo ignora.
  - Los decoradores no cambian: pasan la solicitud tal cual. La respuesta final sigue siendo el texto completo.
- **Redactor.** Lo toma de un `CanalDeRedaccion` scoped que llena el controller.
- **Endpoint.** `POST /api/asistente/consultas/flujo` acepta el mismo pedido y la misma `Idempotency-Key` que `/consultas` y comparte su lógica. Un error de validación responde con el mismo status HTTP de siempre, porque todavía no se escribió nada. Después responde `text/event-stream`:

  | Evento      | Cuerpo                  | Cuándo                           |
  | ----------- | ----------------------- | -------------------------------- |
  | `redaccion` | `{ texto }`             | Cada fragmento                   |
  | `resultado` | `RespuestaDelAsistente` | Al final, siempre                |
  | `error`     | `{ status }`            | Si algo falla después de empezar |

- **Frontend.** Usa el flujo sólo si `GET /capacidades` dice `redaccionEnFlujo: true`. Muestra el texto parcial en el turno en vuelo y lo reemplaza por el resultado final. Sin la opción, nada cambia.

### D10 — Perfil RTX 3070

- **GPU:** 8 GB GDDR6, 448 GB/s, Ampere `sm_86`. El soporte de todos los motores es maduro.
- **Por qué llama-server:** con ~4,7 GiB de pesos de un 8B en 4 bits, vLLM (que reserva memoria para CUDA graphs y activaciones) deja muy poco KV. `llama-server` corre igual en Windows nativo o en Docker con WSL2.
- **Configuración:** Qwen3-8B Q4_K_M, `-np 2`, `-c 26624` (13.312 tokens por slot), KV `q8_0`. Son ~1,9 GiB de KV, unos ~7,3 GiB en total **[estimado]**.
- **Respaldo:** `-np 1`, o Qwen3-4B-Instruct-2507 Q6_K.
- **Perfil del backend:**
  - esquema compacto prendido;
  - ejemplos en el prefijo prendidos — se entregó apagado por falta de lugar en el slot, pero medido el 2026-10-03 el pedido más largo usa 9,8k de los 13.312 tokens y el modelo acierta más con todos los ejemplos que con cuatro elegidos por pregunta (`docs/architecture/modelo-local.md` §8);
  - compuerta en 2;
  - techos de 600 / 300 / 150 tokens.

## Risks / Trade-offs

- **Calidad.** Cada opción es una hipótesis para un modelo chico. El evaluador las tiene que confirmar de a una.
- **La caché de consultas** reutiliza una interpretación: si fue mala, se repite hasta que vence. La vigencia es corta y configurable.
- **Streaming.** Un proxy que bufferea (Traefik, Cloudflare) puede juntar los eventos. Se manda `X-Accel-Buffering: no` y `Cache-Control: no-cache`, y si igual bufferea, el resultado llega completo como hoy.
