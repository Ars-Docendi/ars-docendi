# Diseño — El primer adaptador real del proveedor

## Contexto

`IProveedorDeModelo` existe desde E2 y hasta hoy tiene una sola implementación, `ProveedorSimulado`. Alrededor del puerto ya hay tres decoradores y un contador, encadenados en `ModuleExtensions`:

```
ProveedorConTechoDeLlamadas  →  ProveedorConBreaker (+ timeout)  →  proveedor real
```

Y por fuera de la cadena, dos cosas más: `ReintentoDeTransporte`, un `DelegatingHandler` registrado en el cliente HTTP con nombre `asistente-proveedor`, y `PresupuestoDelTurno`, la cota punta a punta que pone `CapaConversacional`.

Ese andamiaje se construyó **anticipando** este adaptador. El comentario que registra el cliente HTTP lo dice: «Todavía no lo consume nadie —el proveedor real llega con el carril SQL—, pero se registra acá para que esa implementación lo pida por nombre y no tenga que saber nada de reintentos».

El adaptador tiene entonces una sola responsabilidad: **traducir**. Del contrato del puerto al del SDK a la ida, y del SDK al vocabulario de fallas del módulo a la vuelta. Todo lo demás ya está construido y este change no lo toca.

## Decisiones

### D1 — Esto es un brazo del `switch`, no una refactorización del pipeline

El puerto no menciona ningún proveedor y la selección ya es por ambiente (`Asistente__Proveedor`). Sumar Anthropic es una clase y un brazo:

```csharp
ProveedorSimulado.Clave  => new ProveedorSimulado(),
ProveedorAnthropic.Clave => new ProveedorAnthropic(...),
_ => throw new InvalidOperationException(...)
```

Se deja escrito porque es la pregunta que va a volver cuando se evalúe correr un modelo propio: **no hay nada que rehacer**. El adaptador siguiente es otro brazo, y los dos pueden convivir en la misma compilación con ambientes distintos eligiendo uno u otro.

El default sigue siendo el simulado, y a propósito: usar un proveedor real —y gastar— tiene que ser una decisión explícita del ambiente. Los ambientes efímeros de PR dependen de eso, porque no pueden tener clave real.

### D2 — La temperatura no viaja, y el puerto igual la conserva

Los modelos Claude actuales **rechazan `temperature` con 400**: está removido en Opus 5, Sonnet 5 y toda la familia 4.6 en adelante. Mandarla no degrada la respuesta, rompe la llamada.

Podría haberse sacado del puerto. No se hace, y el motivo importa: la temperatura sigue siendo un parámetro real de casi cualquier otro proveedor, incluido un modelo propio corriendo con vLLM o con Ollama, que es la migración que el proyecto contempla. Sacarla del puerto porque _un_ adaptador no la soporta convertiría al puerto en la forma de ese adaptador — que es exactamente lo que un puerto no debe ser.

**El adaptador la absorbe.** El determinismo que el carril SQL necesita se pide de las dos formas que el modelo sí acepta: por instrucción dentro del prefijo estable, que ya está escrito así, y por `OutputConfig.Effort`.

Lo que sí cambia es el comentario de `SolicitudAlModelo.Temperatura`, que hoy promete «0.0 para generar SQL». Con este adaptador esa promesa no se cumple, y un comentario que miente es peor que ninguno.

### D3 — El prefijo estable va como bloque `system` marcado como cacheable

El prompt del carril SQL tiene dos partes y el puerto ya las separa: `PrefijoEstable` —el esquema y las instrucciones, que no mutan por turno (RNF-14)— y `Mensaje`, la pregunta.

Esa separación existe para que el prefijo se cachee del lado del proveedor. Es donde está casi todo el ahorro: el esquema es el bloque más grande del prompt y se repite idéntico en cada turno. Pero **marcarlo es responsabilidad del adaptador**: si viaja como texto común, el diseño sigue siendo correcto y el ahorro simplemente no ocurre, sin que nada falle ni se note salvo en la factura.

Va como `System = new List<TextBlockParam> { new() { Text = prefijo, CacheControl = new CacheControlEphemeral() } }`.

`ProveedorDeEsquema` ya es singleton por esta misma razón —cachea el prefijo para no recalcularlo por request—, así que la mitad del mecanismo ya estaba puesta y le faltaba la otra.

### D4 — El adaptador no reintenta, y apagarlo es obligatorio

El SDK reintenta por defecto: errores de conexión, 408, 409, 429 y 5xx, con backoff exponencial.

El módulo **ya reintenta**, en `ReintentoDeTransporte`, y `OpcionesAsistente.MaximoDeIntentosDeTransporte` documenta el peor caso de un turno como `MaximoDeLlamadasPorTurno × MaximoDeIntentosDeTransporte` = 4 × 3 = 12 requests HTTP, y dice que «las dos cotas explícitas son lo que hace que ese número se pueda decir en voz alta».

Con el reintento del SDK encendido, ese número pasa a 4 × 3 × 3 = 36 y **nada falla**: el sistema simplemente hace el triple de requests de las que su propia documentación declara. Por eso `MaxRetries = 0` no es una preferencia, es lo que sostiene una cota ya escrita.

El adaptador consume el cliente HTTP con nombre `asistente-proveedor`, que ya trae el handler de reintento en su pipeline. Un solo lugar reintenta.

### D5 — Ninguna excepción del SDK sale del adaptador

`ProveedorConBreaker` cuenta exactamente dos formas de fallo:

```csharp
catch (OperationCanceledException) when (propio is { IsCancellationRequested: true })  // timeout
catch (HttpRequestException)                                                            // transporte
```

**Cualquier otra excepción lo atraviesa sin contarse.** Si el adaptador dejara escapar un `Anthropic5xxException`, el breaker nunca abriría: un proveedor caído al cien por ciento seguiría recibiendo llamadas turno tras turno, y la degradación —que es la razón de ser del breaker— no se activaría jamás.

Ensanchar el `catch` del breaker para que conozca los tipos del SDK está descartado: acoplaría un decorador genérico a un adaptador concreto, y el brazo siguiente traería sus propios tipos.

Así que traduce el adaptador, que es el único que tiene por qué saber de Anthropic:

| Falla del SDK                                                                                          | Traducción                             | Por qué                                                                                                                                                     |
| ------------------------------------------------------------------------------------------------------ | -------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Anthropic5xxException`, `AnthropicRateLimitException`, `AnthropicIOException`, `HttpRequestException` | `HttpRequestException`                 | El proveedor no sirvió la llamada. `CarrilSql` ya lo resuelve como degradado y el breaker lo cuenta.                                                        |
| Cancelación por el token propio del breaker                                                            | se deja pasar                          | El breaker la reconoce y la convierte en `TimeoutDelProveedor`. Atraparla acá le sacaría el fallo de las manos.                                             |
| `AnthropicUnauthorizedException`, `AnthropicForbiddenException`                                        | log **Error** + `HttpRequestException` | Ver D6.                                                                                                                                                     |
| `AnthropicBadRequestException`, `AnthropicUnprocessableEntityException`                                | log **Error** + `HttpRequestException` | Es un defecto nuestro de armado del request. Se degrada para no romperle el turno al usuario, pero se loguea como error porque ningún reintento lo arregla. |

### D6 — La credencial rechazada degrada como todo lo demás, pero se loguea como error

Una clave vencida o mal cargada hace fallar todas las llamadas. Hay dos formas de tratarla y las dos tienen un costo:

- **Como falla de transporte a secas**: el usuario recibe la degradación cortés que el contrato promete, pero en los logs parece intermitencia normal del proveedor. Una clave mal cargada puede quedar así días.
- **Como excepción propia que atraviesa el pipeline**: se vuelve imposible de ignorar, pero cada request termina en 500 — y el módulo tiene un contrato de cuatro estados justamente para que un problema del proveedor nunca sea un 500.

Se toman las dos mitades buenas: **se degrada** —el usuario ve lo que el contrato promete— y el adaptador **loguea en Error, con un mensaje que nombra la causa**, antes de traducir. El operador tiene una señal inequívoca y el usuario no ve una pantalla rota.

### D7 — Una respuesta sin texto no es una caída

Un modelo puede contestar sin bloque de texto: se rehusó (`stop_reason: "refusal"`), o pensó y no escribió. Eso **no es** el proveedor caído, y tratarlo como tal abriría el breaker por algo que no es una falla de servicio.

El adaptador devuelve `RespuestaDelModelo` con texto vacío. El pipeline ya sabe qué hacer: el validador rechaza una SQL vacía y el turno abstiene. Abstener es la respuesta correcta a «el modelo no produjo nada», y es lo que la métrica del asistente premia.

El rehúso se loguea en Warning con su categoría, porque saber que el modelo se rehusó —y no que devolvió algo inválido— cambia qué se investiga.

### D8 — La clave llega por configuración y su ausencia falla al pedir el proveedor, no al arrancar

Mismo patrón que las cadenas de solo lectura, y por el mismo motivo escrito ahí: construirlas al arrancar rompería el Host en cualquier ambiente que todavía no las tenga. El proveedor ya se registra como fábrica; la validación de la clave va adentro de esa fábrica, y el mensaje nombra el valor faltante.

La clave **nunca** se escribe al repositorio, no entra en `appsettings.json` y no aparece en ningún log: `RegistroDelTurno` registra el nombre del proveedor, no su credencial.

### D9 — El modelo y el esfuerzo son configuración, no constantes

`Asistente__Modelo` y `Asistente__Esfuerzo`, con default. Cambiar de modelo —para medir costo contra calidad en los cuatro ejes de evaluación, que es justamente lo que TD-008 destraba— tiene que ser una variable de ambiente y no un recompilado.

## Lo que este change no hace

- **No elige el proveedor de producción.** Deja el primer adaptador andando y el seam probado. La decisión de con qué se corre sigue abierta.
- **No corre la evaluación real.** Eso necesita una clave y presupuesto, y es la otra mitad de TD-008.
- **No agrega streaming.** El contrato del puerto devuelve el texto completo, y el frontend ya muestra progreso sin él.
