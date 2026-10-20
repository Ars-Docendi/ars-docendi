## Context

Develop agregó un servicio de inferencia compartido (`servicio-ollama-compartido`). Es Ollama en la RTX 5070 (12 GB) de pc-prod con `qwen3.5:9b`. Lo publica Traefik en un entrypoint privado y Cloudflare Access lo protege afuera de prod. También agregó un cliente mínimo, `ArsDocendi.Inferencia` (`integrar-cliente-inferencia`), que sólo comprueba que el servicio responda.

El asistente ya tiene un adaptador para un servidor propio (`ProveedorLocal`, `asistente-proveedor-local`), pero habla la API OpenAI. El servicio compartido deja pasar sólo la API nativa de Ollama. La investigación de fondo sobre el modelo, la VRAM y la capacidad está en [`docs/architecture/modelo-local.md`](../../../docs/architecture/modelo-local.md). La configuración del servidor está en [`docs/operations/ollama-compartido.md`](../../../docs/operations/ollama-compartido.md).

Cuatro datos del servicio ordenan las decisiones:

- **API y autenticación.** Traefik sólo deja pasar `GET /api/version`, `GET /api/tags`, `POST /api/chat` y `POST /api/generate`.
  - Pide BasicAuth con un usuario por ambiente (`prod`, `staging`, `preview`).
  - Afuera de prod, Cloudflare Access pide además el Service Token y una IP de origen permitida (la de vm-dev).
- **Un pedido a la vez.** Ollama fuerza `NUM_PARALLEL=1` para la arquitectura de qwen3.5. Ese único slot lo comparten todos los ambientes.
  - Con la cola llena (`OLLAMA_MAX_QUEUE`), Ollama responde 503.
  - Delante, Traefik limita a 30 pedidos por minuto y 4 en vuelo, por IP de origen.
- **Contexto chico.** El servidor arranca con `OLLAMA_CONTEXT_LENGTH=4096`.
  - El prefijo estable del asistente ronda los 10–12k tokens.
  - Ollama **no falla** cuando el prompt no entra: conserva el principio de la conversación (`num_keep`) más la cola del prompt y descarta el medio, que acá es el esquema. El modelo contestaría sin el esquema, sin ningún error.
- **Corte del borde.** Cloudflare corta en ~100 s una respuesta de origen que no empezó a llegar (error 524).

## Goals / Non-Goals

**Goals**

- Que el asistente funcione contra el servicio compartido sin tocar el pipeline ni los demás proveedores.
- Un solo cliente del servicio, con las credenciales y la validación que ya existen.
- Poder probarlo en un preview de PR sin tocar staging.

**Non-Goals**

- **Decidir que `qwen3.5:9b` es suficiente.** Lo decide el evaluador (`backend/eval`) corrido contra el servicio (tarea 5.2), igual que en `asistente-proveedor-local`.
- **Cambiar la configuración del servidor.** La opera infraestructura en pc-prod, en una ventana aprobada. Este change documenta lo que el asistente necesita (D6), pero no edita `infra/ollama/`.
- **Regrabar cassettes contra Ollama.** `ClaveDeCassette` hoy espera el cuerpo de Anthropic, con `system` en la raíz. Grabar contra Ollama necesita su propio change (TD-030).

## Decisions

### D1 — Adaptador nuevo contra `/api/chat` y no `ProveedorLocal` con otra URL

Ollama también expone `/v1/chat/completions`, pero Traefik no lo deja pasar. Abrirlo cambiaría la superficie de un servicio compartido para ahorrar un adaptador.

Además, la API nativa da cosas que la compatible no da:

- `think` por pedido (en `/v1` el razonamiento de qwen3.5 queda prendido por defecto);
- `truncate: false` (D3);
- `prompt_eval_cached_count` para medir la caché de prefijo.

`ProveedorOllama` reutiliza el transporte del módulo:

- `ClienteDelProveedor`, con sus reintentos de transporte y la compuerta de concurrencia;
- `ProveedorLocal.VisibleHastaAhora` y `SinRazonamiento`, para limpiar el texto.

Las credenciales se ponen en cada pedido (`OpcionesInferencia.Autenticar`) y no en el `HttpClient` compartido. Así no pueden viajar a Anthropic ni a un servidor `local`, y hay un test que lo comprueba.

### D2 — `ArsDocendi.Inferencia` se integra a `Modules.Asistente`

El proyecto tenía un solo consumidor, el endpoint de estado. El único consumidor previsto del modelo es el asistente.

Mantenerlo aparte obligaba a elegir entre dos malas opciones:

- una arista nueva `Modules.Asistente → ArsDocendi.Inferencia`, con un contrato público para un único llamador;
- dos clientes del mismo servicio con la misma configuración.

Se mueven `OpcionesInferencia`, `ClienteInferencia` y `GET /api/inferencia/estado` al módulo, **sin cambiar** nada de esto:

- las claves `Inferencia:*` de cada ambiente;
- la validación al arranque, que sigue apagada en Development;
- la ruta, el permiso `sistema.estado.ver` y el contrato;
- `PortalHttpTests`.

El grafo pierde un nodo y no gana ninguna arista. Si mañana otro módulo necesita el modelo, el cliente pasa a un `Contracts`. No vuelve a ser un proyecto suelto.

### D3 — `num_ctx` fijo por configuración y `truncate: false`

`Asistente:ContextoDelModelo` (0 = no mandar) va en cada pedido como `options.num_ctx`.

El valor tiene que **coincidir** con el `OLLAMA_CONTEXT_LENGTH` del servidor. Un `num_ctx` distinto del cargado hace que Ollama recargue el modelo, unos segundos por pedido, y en un servidor compartido ese costo lo paga el pedido de otro.

El perfil usa 16384:

- el prefijo (~12k) más la parte variable (≤1,3k) más la salida (≤800) entran con margen;
- según la estimación del KV cache de qwen3.5-9B (~32 KiB/token, sólo 8 capas de atención completa), son ~8–8,5 GB de VRAM.

`truncate: false` convierte el recorte silencioso del contexto en un 400 que llega al pipeline como falla de transporte. El log sugiere revisar el contexto. Un turno degradado se ve; una consulta generada sin esquema, no.

### D4 — Concurrencia 1 por ambiente, saturación separada de la caída

El servidor atiende un pedido por vez para todos los ambientes. Más de un pedido en vuelo por backend sólo llena la cola de Ollama y la de Traefik. Por eso el perfil pone `MaximoDeLlamadasConcurrentes=1` y `EsperaMaximaEnColaSegundos=45`, y el resto espera en la compuerta del módulo.

Cómo se clasifican las respuestas del servidor:

| Respuesta                                      | Qué significa                 | Cómo se loguea                     |
| ---------------------------------------------- | ----------------------------- | ---------------------------------- |
| 429 (Traefik) y 503 (Ollama con la cola llena) | Saturación                    | Se distingue de la caída en el log |
| 401/403                                        | BasicAuth, Service Token o IP | Se nombran las tres causas         |
| 404                                            | Modelo no descargado          | Se dice eso                        |

`MaximoDeIntentosDeTransporte=2`: reintentar contra un servidor saturado sólo le suma carga.

### D5 — Perfil de deploy y preview por label

- **`compose.asistente-ollama.yml`.** Repite los techos y optimizaciones del perfil `local`, pensados para un modelo chico. Agrega:
  - el contexto (D3) y la concurrencia (D4);
  - `PresupuestoDelTurnoSegundos=90`, debajo del corte de Cloudflare;
  - `StreamingDeRedaccion`, que funciona porque Traefik no bufferea (`flushinterval -1ms`).
- **`spin-up.sh`.** Con `ollama` pide `INFERENCIA_URL/USUARIO/CONTRASENA` en vez de `ASISTENTE_CLAVE`. Sin ellas degrada a `simulado`, como un proveedor real sin clave.
- **Workflow de deploy.** La label `asistente-ollama` en un PR elige `ollama` sin tocar el secret del Environment. Staging sigue con su proveedor y cada preview se prende a demanda.

### D6 — Lo que el asistente necesita del servidor (sin aplicarlo acá)

Lo que se recomienda al operador está en [`ollama-compartido.md`](../../../docs/operations/ollama-compartido.md#11-configuración-para-el-asistente). En resumen:

- **Imagen de Ollama ≥ 0.31.2.** Antes de esa versión, `format` junto con `think: false` ignora el esquema en qwen3.5 (ollama/ollama#14645).
- **`OLLAMA_CONTEXT_LENGTH=16384`**, igual a `ContextoDelModelo`.
- **`OLLAMA_KEEP_ALIVE=24h`**, para no pagar la carga del modelo después de 5 minutos sin uso.
- **`OLLAMA_FLASH_ATTENTION=1`.**
- **Tasa de Traefik por un criterio que distinga ambientes.** Detrás de cloudflared, todos los pedidos llegan con la misma IP remota. Un turno hace hasta 4 llamadas, así que 30 por minuto compartidos entre staging y todos los previews alcanzan para unos 8 turnos por minuto en total.

### D7 — El servicio compartido es hardware propio para la redacción

La GPU de pc-prod es del Departamento, así que `ollama` cuenta como `local` para `RedaccionSinEnmascarar` (`asistente-redaccion-sin-enmascarado-local`). El perfil la prende, y siguen sin cambiar las otras condiciones: sin cassettes, `sensible-texto` siempre suprimido, y quién lee datos personales lo decide PostgreSQL.

Hay un único lugar que decide la vigencia, `OpcionesAsistente.RedaccionSinEnmascararVigente`, y ahora acepta los dos proveedores.

Qué recorren los valores reales en cada ambiente:

- **Prod:** llega al servicio por la red Docker local de pc-prod y no pasa por Internet.
- **Staging y previews:** pasan por Cloudflare, que termina TLS en el borde. Esos ambientes sólo tienen datos sintéticos (nunca SGA ni PII real), así que no viaja ningún valor real.

Los valores quedan en la caché KV del único slot de Ollama, que es memoria de la GPU propia, y Ollama no los persiste.

## Risks / Trade-offs

- **[Caché de prefijo menos efectiva que en vLLM]**
  - Qwen3.5 es híbrido (Gated DeltaNet más atención). Ollama reutiliza el prefijo mediante checkpoints del estado recurrente cerca del final del prompt, y el único slot alterna entre tres prompts de sistema distintos (generación, redacción y reescritura), que se desalojan entre sí.
  - Estimado: un prompt de 12k sin caché tarda 2,5–6 s de prefill.
  - → `prompt_eval_cached_count` se mapea a los tokens de caché del módulo. El panel y el evaluador miden cuánto se reutiliza de verdad.
- **[Servidor compartido con prod]**
  - Un evaluador o un preview pueden ocupar el único slot que usa prod. Ollama no tiene prioridades.
  - → Concurrencia 1 por ambiente; el evaluador corre con un usuario propio y en una ventana acordada (tarea 5.2).
- **[Calidad no medida]**
  - Los benchmarks públicos de Qwen3.5-9B frente a Qwen3-8B son mejores en seguimiento de instrucciones (IFEval, IFBench), pero no hay números de text-to-SQL.
  - → Tarea 5.2 antes de pasar staging a `ollama`.
- **[Sin cassettes]**
  - El corpus grabado sigue siendo de Anthropic, y los tests del adaptador usan un servidor falso.
  - → TD-030.

## Migration Plan

1. Mergear este change. No cambia nada en ningún ambiente: ninguno tiene `ASISTENTE_PROVEEDOR=ollama`.
2. Infraestructura aplica la configuración recomendada (D6) en pc-prod, en una ventana aprobada.
3. Abrir un PR con `deploy-preview` + `asistente-ollama`, comprobar «Inferencia (IA)» disponible y hacer un turno real (tarea 5.1).
4. Correr el evaluador contra el servicio (tarea 5.2).
5. Sólo con esa medición, decidir si staging pasa a `ollama` cambiando el secret `ASISTENTE__PROVEEDOR`.

Rollback: quitar la label, o volver el secret a su valor anterior y redeployar. No hay datos ni migraciones involucrados.
