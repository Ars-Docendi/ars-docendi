# Modules.Asistente

Módulo del **asistente conversacional**: responde preguntas en lenguaje natural
sobre datos que ya viven en la base del sistema, en modo solo lectura.

Definición funcional y arquitectura objetivo en
[docs/product/designs/asistente-conversacional-definicion.md](../../../docs/product/designs/asistente-conversacional-definicion.md).
Change de planning: `openspec/changes/asistente-fundaciones/`.

## Estado

**Carril SQL construido, sin superficie de usuario.** El módulo traduce una
pregunta en español a una consulta, la valida, la ejecuta acotada al actor y
redacta la respuesta. `CarrilSql` es un servicio, no un endpoint.

Lo que falta y dónde va:

| Qué                                                          | Épica |
| ------------------------------------------------------------ | ----- |
| Enmascaramiento de columnas sensibles                        | E4    |
| Hilo conversacional, reescritor y detector de ambigüedad     | E5    |
| Carril determinista de API vía `Modules.<X>.Contracts`       | E6    |
| `POST /api/asistente/consultas` y contrato de cuatro estados | E7    |
| Cuota por actor, circuit breaker y registros                 | E8    |

No hay carpeta `Domain/`, a diferencia de los otros módulos: el asistente no
tiene entidades propias — lee las de otros schemas y orquesta. Si algún día
aparece un agregado que le pertenezca, se crea entonces.

## La capa conversacional

Va encima del carril, no adentro. `CarrilSql.ResponderAsync` ya recibía una pregunta
autocontenida; la capa es quien la calcula.

```
CapaConversacional.ResponderAsync(actor, hilo, mensaje)
  │
  ├─ IAlmacenDeHilos        ──► hilo en memoria, TTL 2 h, atado al actor
  │                             guarda PREGUNTAS, nunca filas
  │
  ├─ EnrutadorSocial        ──► CARRIL SIN DATOS · 0 tokens
  │                             se SALTEA si hay aclaración pendiente
  │
  ├─ ReconocedorDeAclaracion──► etiqueta → token distintivo → ordinal · 0 tokens
  │
  ├─ DetectorDeCambioDeTema ──► al marcarlo, SUELTA el segmento
  │                             (el reescritor queda sin historial que arrastrar)
  │
  ├─ ReescritorDePreguntas  ──► única llamada al modelo de la capa; solo con historial
  │
  ├─ DetectorDeAmbiguedad   ──► necesita_aclaracion · 0 tokens · un SELECT, no el modelo
  │
  └─ CARRIL SQL (abajo)
```

**El pivote se fuerza.** Al detectar cambio de tema no se le pide al modelo que
ignore el historial: se suelta el segmento y el reescritor no recibe ninguno. Por
eso el test del pivote no mira la salida del modelo, mira qué se le mandó.

**«¿y en Sistemas?» no es un pivote**, aunque nombre una entidad que no está activa.
Lo salva la guarda del marcador anafórico, que va antes que la de entidad.

**El detector de ambigüedad no se extiende a la vaguedad.** Dispara solo ante una
colisión verificada por consulta. Preguntar tiene un costo medido: las aclaraciones
de calidad baja son peores que no preguntar.

## El carril SQL

Dos llamadas al modelo por turno y ocho piezas deterministas alrededor. La
asimetría es deliberada: cada pieza determinista que se agrega al medio es una
pieza que no puede alucinar.

```
CarrilSql.ResponderAsync(actor, mensaje, preguntaInterpretada)
  │
  ├─ IPerfilDelActor        ──► alcance global · acceso a datos personales
  │                             (valida el actor: un oid de Azure falla acá)
  │
  ├─ GeneradorDeSql         ──► LLAMADA 1 · temperatura 0 · prefijo cacheado
  │    ├─ IProveedorDeEsquema  prefijo estable, derivado de los GRANT efectivos
  │    ├─ ISelectorDeEjemplos  ejemplos por similitud léxica, en el prompt de usuario
  │    └─ IFechaDeReferencia   «hoy» como parámetro, nunca now()
  │
  ├─ ValidadorDeSql         ──► rechazo → no contestable, SIN reintento ciego
  │
  ├─ IEjecutorDeConsulta    ──► transacción nueva READ ONLY, actor transaction-local,
  │                             LIMIT tope+1, fila sonda descartada
  │                             clasifica cada columna por (OID, attnum) del motor
  │                             42501 del motor → abstención, nunca error crudo
  │
  ├─ PoliticaDeAbstencion   ──► vacío + actor global → un reintento de generación
  │                             vacío + actor acotado → respuesta SIN segunda llamada
  │
  ├─ Enmascarador           ──► FRONTERA DE SALIDA · lo que va al modelo va tapado,
  │                             lo real sigue viaje al llamador
  │
  └─ RedactorDeRespuesta    ──► LLAMADA 2 · temperatura 0,3 · sin caché
```

### La frontera de salida

Los `GRANT` deciden quién puede **leer** qué. El enmascarador decide qué **sale
hacia un tercero**, que es otra pregunta: un actor puede tener todo el derecho a
ver un teléfono en pantalla y no haber ninguna razón para que llegue al proveedor.

`database/asistente/manifiesto-sensibilidad.json` clasifica cada columna legible en
`publica`, `sensible-valor` (al modelo va `«documento 1»`, el valor real va al
llamador) o `sensible-texto` (se suprime la columna entera, nombre incluido).

La columna se identifica por el par `(OID de tabla, número de atributo)` que emite
el motor, **no por su nombre en el resultado**: ese nombre es el alias que eligió la
consulta generada, y un `SELECT p.documento AS codigo` lo dejaría pasar entero.

El marcador es un contador por orden de aparición, no un hash del valor: un hash de
un documento se invierte por fuerza bruta en segundos.

**Es asimétrico**: la pregunta cruda del usuario viaja al proveedor a través de la
generación. Protege el camino de vuelta, no el de ida.

El caso que el par no cubre —una expresión sobre una columna personal— está en
TD-009.

### Cómo se agrega un ejemplo al catálogo

`Recursos/ejemplos-sql.json`, embebido como recurso del assembly. Cada entrada
lleva `pregunta`, `sql` y `categoria`.

Tres cosas se verifican solas al agregarlo:

- la consulta **ejecuta** contra el esquema vigente (`CarrilSqlTests`);
- la consulta **pasa el validador** (`SelectorDeEjemplosTests`) — un ejemplo que el
  propio validador rechazaría le estaría enseñando al modelo a escribir consultas
  que después se van a rechazar;
- la huella del catálogo cambia, y con ella el sellado de los reportes de
  evaluación.

**Invariante que hay que sostener a mano**: el catálogo y el dataset de capacidad
son disjuntos. Si se solapan, la métrica mide cuán bien el sistema reproduce
ejemplos que ya vio — y como el catálogo de capacidades deriva sus sugerencias de
acá, el asistente estaría proponiendo las preguntas con las que se lo evalúa.

### Cuándo se recalcula el prefijo

**Al reiniciar el proceso, y solo entonces.** El prefijo se construye la primera
vez que alguien lo pide, se cachea por rol y no se invalida por su cuenta.

Es deliberado. Un prefijo que se invalidara solo podría cambiar entre dos turnos
consecutivos —lo que RNF-14 prohíbe— y cada invalidación pagaría escritura de
caché a 1,25× en vez de lectura a 0,1× sobre el bloque más grande del prompt.

Consecuencia operativa: **una migración que cambie el esquema exige reiniciar**.
El despliegue ya lo hace. Y el hash del prefijo va sellado en cada reporte de
evaluación, así que una corrida contra un esquema viejo queda registrada como tal.

Los dos roles tienen prefijos distintos, con huellas distintas: el prefijo se
deriva de los privilegios **efectivos** de cada conexión, no de una lista en el
código.

## Endpoints (superficie HTTP)

- `GET /api/asistente/ping` — smoke test, `[AllowAnonymous]`. No toca la base ni
  ningún servicio externo: tiene que poder distinguir «el módulo está cargado» de
  «la base responde».

### El contrato de respuesta

`opciones` y `sugerencias` son campos **separados**. Las opciones bloquean el turno
esperando una elección; las sugerencias no bloquean nada. Un solo campo obligaría a la
interfaz a adivinar cuál le llegó mirando el estado, y el día que un turno respondido
quiera sugerir algo la distinción se pierde del todo.

`estado` viaja con etiquetas propias del contrato (`respondida`, `no_contestable`,
`necesita_aclaracion`, `servicio_degradado`) y no con el nombre del enum: renombrarlo
adentro no puede romper a los clientes en silencio.

`metricas.categoria` es más fina que `estado`. Una generación cortada por el techo de
tokens llega como `no_contestable` con el mismo texto que una abstención, pero con
`categoria = truncado_en_generacion`: el registro operativo guarda el estado y ve una
abstención más; el analítico guarda la categoría y las distingue. Es lo que le permite
al evaluador no acreditar como abstención correcta un turno en que el modelo no
decidió nada.

`sql` solo viaja con `asistente.ver_consulta`, y el chequeo está donde se arma la
respuesta —no en el controller—, para que cualquier camino nuevo lo herede.

### La idempotencia

En memoria, acotada por **(actor, clave)** y con expiración corta. La clave sola
alcanzaría para el doble clic y sería un canal de fuga: dos usuarios que manden la misma
—cosa que pasa, los clientes generan claves y nada garantiza que no colisionen—
compartirían respuesta, y el segundo recibiría datos calculados con el alcance del
primero.

No se reusa ni se copia `designaciones.idempotencia_comandos`: guarda el `response_body`
completo, que es exactamente lo que este módulo decidió no persistir.

### El catálogo de capacidades

Scoped, con la caché singleton al lado. Los alcances no son intercambiables: el catálogo
depende de `IPerfilDelActor`, que resuelve al actor del turno, y un catálogo singleton
capturaría el perfil del primer actor que consultara. El contenedor rechaza esa
registración al arrancar, y hace bien.

Lo que sí sobrevive al request es el resultado de leer el catálogo de PostgreSQL, y eso
vive en `CacheDeCapacidades`, indexado por **rol**: hay exactamente dos variantes y los
`GRANT` no cambian en runtime.

## Proveedor del modelo

`IProveedorDeModelo` (en `Application/`) es la interfaz propia detrás de la cual
vive el proveedor de LLM. No menciona ningún proveedor: `PrefijoEstable`,
`Mensaje`, `Temperatura` y `MaximoDeTokens` de ida; texto y conteo de tokens de
vuelta.

El `switch` de `ModuleExtensions` **es el registro de adaptadores** y la selección
va por ambiente (`Asistente:Proveedor`):

| Clave       | Implementación       | Cuándo                                                |
| ----------- | -------------------- | ----------------------------------------------------- |
| `simulado`  | `ProveedorSimulado`  | Default de todos los ambientes. Determinista, sin red |
| `anthropic` | `ProveedorAnthropic` | Requiere `Asistente:ClaveDelProveedor`                |

Sumar uno nuevo —otro proveedor, o un modelo propio corriendo en la nube— es una
clase en `Infrastructure` y un brazo más del `switch`. No hay nada del pipeline que
rehacer, y los dos conviven en la misma compilación con ambientes distintos
eligiendo uno u otro.

El default es el simulado y usar uno real exige configuración explícita. El motivo
no es estilístico: los ambientes efímeros de PR no pueden tener clave real, porque
su workflow hace checkout del head del pull request y ejecuta un script que viene de
ese mismo PR, en un job con los secrets del environment.

La respuesta simulada se identifica como tal en la bandera `EsSimulada` **y** en el
texto. Un proveedor de mentira que devolviera algo verosímil sería peor que uno que
falla: la métrica del asistente es corrección con abstención.

### Configuración del adaptador real

| Variable                           | Default           | Qué decide                                    |
| ---------------------------------- | ----------------- | --------------------------------------------- |
| `Asistente__Proveedor`             | `simulado`        | Cuál adaptador se construye                   |
| `Asistente__ClaveDelProveedor`     | vacío             | La credencial. Nunca en un archivo versionado |
| `Asistente__Modelo`                | `claude-sonnet-5` | Qué modelo                                    |
| `Asistente__EsfuerzoDeGeneracion`  | `medio`           | Deliberación al generar la consulta           |
| `Asistente__EsfuerzoDeRedaccion`   | `bajo`            | Deliberación al redactar en español           |
| `Asistente__EsfuerzoDeReescritura` | `bajo`            | Deliberación al reescribir un seguimiento     |

**Los esfuerzos son tres y no uno, y el motivo es de latencia.** Con un valor
global, la redacción deliberaba antes de escribir la primera palabra: para quien
preguntó eso es espera pura, porque las filas ya estaban. Medido sobre una corrida
completa de los cuatro ejes, separarlos bajó el p95 de 9,4 s a 6,7 s **sin mover
ninguno de los cuatro puntajes**.

La generación se queda en `medio` a propósito: elegir el join correcto entre
catorce tablas es el trabajo que sí mejora deliberando, y es la llamada donde
equivocarse produce una respuesta falsa.

Valores aceptados: `minimo`, `bajo`, `medio`, `alto`, `maximo`. Uno mal escrito
falla al resolver el proveedor y nombra cuál de los tres es.

**Por qué esos dos defaults.** El esquema que el modelo maneja es chico —catorce
tablas, poco más de cien columnas— y ése es el factor que más pesa en traducir
preguntas a SQL, así que Opus no se paga. Hacia abajo tampoco conviene: Haiku 4.5
no acepta el parámetro de esfuerzo (cada llamada volvería `400`) y su retiro está
anunciado para no antes de octubre de 2026.

El esfuerzo va en `medium` por costo **y** por un riesgo concreto: los modelos
actuales piensan por defecto, esos tokens se facturan como salida y cuentan contra
el techo de la llamada (`MaximoDeTokensDeGeneracion`). Con esfuerzo alto el modelo
puede gastar el presupuesto pensando y cortar el JSON antes de cerrarlo. Para el
usuario eso es «no pude interpretar la pregunta», el mismo texto que una abstención
genuina (RNF-18: nada de presupuestos ni de formatos), pero **ya no se confunde con
una**: cuando el proveedor declara que paró por presupuesto y el objeto no se puede
interpretar, el turno sale con `categoria = truncado_en_generacion`, y el evaluador
lo cuenta aparte —ni acierto ni abstención—. Si esa fila aparece en el reporte, el
techo es lo que hay que subir. Ya no es una hipótesis que se infiere de abstenciones
sin explicación: se mide.

Las dos elecciones se confirman o se corrigen con una corrida del evaluador, que
para eso existe. Cambiar de modelo entre corridas es una variable de ambiente.

Para levantarlo en desarrollo con clave real, sin escribirla a ningún archivo:

```bash
export Asistente__Proveedor=anthropic
export Asistente__ClaveDelProveedor=...
dotnet run --project backend/src/ArsDocendi.Host
```

Faltando la clave, el Host **arranca igual** y el ping responde: el error llega
recién a quien pida el proveedor, y nombra el valor que falta. Un ambiente a medio
configurar tiene que poder levantar.

### Los cassettes del proveedor

Graban el cuerpo **crudo** de la respuesta del proveedor y lo vuelven a servir
desde disco, con el mismo criterio que cualquier suite de fixtures VCR: si el
cassette existe y la variable de re-grabación no está, se lee del disco; si no
está, se llama a la API real y se graba.

| Variable                           | Default | Qué decide                                                            |
| ---------------------------------- | ------- | --------------------------------------------------------------------- |
| `Asistente__DirectorioDeCassettes` | vacío   | Dónde viven los cassettes. **Vacío apaga el mecanismo entero**        |
| `Asistente__RegrabarCassettes`     | vacío   | Cualquier valor no vacío permite salir a la red a grabar lo que falte |

**Con el directorio vacío el handler ni siquiera se registra**, así que el
pipeline del cliente HTTP queda idéntico al de antes de que el mecanismo
existiera: producción no paga nada y no hay nada que se pueda misconfigurar. La
única forma de encenderlo es escribir una ruta.

**Con una ruta puesta y sin la variable de re-grabación, una llamada sin cassette
falla y NO sale a la red.** Es lo que hace imposible que el CI gaste plata por
este camino (RNF-15, RNF-16): el handler lanza sin invocar hacia adentro, y el
error nombra la clave que faltó y el directorio donde se la buscó.

**Con el cassette presente no se re-graba**, aunque la variable esté puesta.
Re-grabar es una operación deliberada sobre las claves que faltan, no un modo en
que cada corrida vuelva a pagar por respuestas que ya están en disco.

Cada cassette lleva un sello con el modelo, la fecha, el hash del prefijo y el
hash del fixture contra el que se grabó. Uno cuyo sello no corresponda al prefijo
o al fixture vigentes **se rechaza en vez de servirse**: la respuesta que guarda
la dio el modelo sobre otro esquema o sobre otros datos.

> **Un cassette prueba el parseo, no la calidad.** Congela una respuesta, no la
> competencia del modelo: lo que estos tests cubren es que
> `GeneradorDeSql.Interpretar`, el redactor y el reescritor sepan leer lo que un
> modelo real devolvió. Si la traducción es buena o mala lo mide el evaluador, y
> nada de esto lo reemplaza.

### Levantar el asistente entero en local

El asistente es el único módulo que necesita **dos roles de PostgreSQL extra**, y
esos roles no los crea ninguna migración: tienen que existir antes, porque las
migraciones les conceden privilegios. Los crea `infra/scripts/provision-db.sh`,
que corre `psql` en un contenedor efímero adjunto a la red `arsdocendi-datos`.

Verificado de punta a punta con el ambiente `pr-25`:

```bash
# 1. Postgres y la red que los scripts esperan
docker network create arsdocendi-datos
docker run -d --name arsdocendi-local --network arsdocendi-datos \
  -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres \
  -p 55432:5432 postgres:18-alpine

# 2. Base + los tres roles (app, asistente básico, asistente con PII)
PGHOST=arsdocendi-local PGPORT=5432 PGUSER=postgres PGPASSWORD=postgres \
APP_DB_USER=app_pr_25 APP_DB_PASSWORD=... \
ASISTENTE_RO_PASSWORD=... ASISTENTE_RO_PII_PASSWORD=... \
  infra/scripts/provision-db.sh pr-25

# 3. Migraciones de los cinco módulos, incluidos los GRANT del asistente
ConnectionStrings__ArsDocendi="Host=localhost;Port=55432;Database=arsdocendi_pr_25;Username=app_pr_25;Password=..." \
Asistente__RolSoloLectura=asistente_ro_pr_25 \
Asistente__RolSoloLecturaPii=asistente_ro_pii_pr_25 \
Asistente__PasswordSoloLectura=... Asistente__PasswordSoloLecturaPii=... \
  dotnet run --project backend/src/ArsDocendi.Host -- --migrate

# 4. Datos sintéticos
PGHOST=arsdocendi-local PGPORT=5432 PGUSER=postgres PGPASSWORD=postgres \
  infra/scripts/seed.sh pr-25

# 5. El Host, con identidad de desarrollo
ASPNETCORE_ENVIRONMENT=Development DevelopmentAuthentication__Enabled=true \
  <las mismas variables del paso 3> \
  dotnet run --project backend/src/ArsDocendi.Host --no-launch-profile

# 6. El frontend, que llega a la API por el proxy de Vite
VITE_API_PROXY_TARGET=http://localhost:5099 pnpm --filter frontend dev
```

Dos cosas que cuestan una tarde si no están escritas:

- La sección de la identidad de desarrollo es `DevelopmentAuthentication`, no
  `AutenticacionDesarrollo`. Con el nombre equivocado el flag queda en `false`,
  no se registra ningún esquema de autenticación, y **todo endpoint protegido
  responde 500 en lugar de 401** — el error no dice que falte configuración.
- El navegador tiene que hablar con el mismo origen. El Host no declara CORS y no
  tiene por qué: en los ambientes desplegados Traefik publica la API bajo `/api`
  en el mismo host. En desarrollo eso lo resuelve el proxy de `vite.config.ts`.

### Qué absorbe el adaptador, y por qué eso es su trabajo

- **La temperatura no viaja.** Los modelos Claude actuales la rechazan con 400. El
  puerto la conserva porque otros proveedores sí la usan; el determinismo del
  carril SQL se pide por instrucción del prefijo y por `Esfuerzo`.
- **El prefijo va marcado para cachear.** Es el bloque más grande del prompt y se
  repite idéntico turno a turno. Sin la marca nada falla: el ahorro simplemente no
  ocurre, y solo se nota en la factura.
- **El adaptador no reintenta.** El SDK lo hace por defecto; se apaga. El reintento
  vive en `ReintentoDeTransporte`, y con los dos encendidos el peor caso documentado
  de un turno pasaría de 12 requests a 36 sin que nada falle.
- **Ninguna excepción del SDK sale de él.** `ProveedorConBreaker` cuenta dos formas
  de fallo —su propia cancelación y `HttpRequestException`— y cualquier otra lo
  atraviesa sin contarse. Un tipo del SDK que se escapara haría que el corte no
  abriera nunca.

Un test de arquitectura fija que el SDK se nombre en **un solo archivo**. Es lo que
hace verificable —y no meramente intencional— la promesa de que el puerto es
agnóstico.

## Reintento y techo de llamadas

Dos cotas explícitas, y explícitas porque **se multiplican**:

| Cota                               | Default | Dónde                        |
| ---------------------------------- | ------- | ---------------------------- |
| Llamadas al modelo **por turno**   | 4       | `ContadorDeLlamadasDelTurno` |
| Intentos de transporte por llamada | 3       | `ReintentoDeTransporte`      |

Peor caso de un turno: `4 × 3 = 12` requests HTTP. El número se puede decir en voz
alta justamente porque las dos cotas están escritas.

El techo de llamadas es **global del turno, no por capa**. Repartido por capa, cada
una respeta su límite y el total se multiplica igual — que es el modo de falla del
que este requisito nace. Lo aplica un decorador sobre `IProveedorDeModelo`, así que
ninguna capa puede saltearlo sin dejar de usar el proveedor.

El reintento de transporte va como `DelegatingHandler` del cliente HTTP, con
backoff exponencial y **jitter completo**, y honra `retry-after` cuando viene. No
reintenta ningún `400` —incluido el del límite de gasto: reintentar un rechazo por
presupuesto agotado gasta presupuesto que ya no hay— ni `401`/`403`, porque una
credencial no se arregla esperando.

Un reintento de transporte ocurre **dentro** de una llamada y no consume cupo del
turno: para eso tiene su propio máximo de intentos.

## Presupuesto, degradación y registros

### Configuración

| Opción                         | Default | Qué acota                                                        |
| ------------------------------ | ------- | ---------------------------------------------------------------- |
| `MaximoDeLlamadasPorTurno`     | 4       | Llamadas al modelo de un turno, global y no por capa             |
| `MaximoDeIntentosDeTransporte` | 3       | Intentos de red **dentro** de una llamada                        |
| `PresupuestoDelTurnoSegundos`  | 150     | El turno completo, punta a punta (RNF-09). Cero lo deja sin cota |
| `TimeoutDeLlamadaSegundos`     | 60      | Una llamada al proveedor                                         |
| `CupoDeLlamadasPorActor`       | 60      | Llamadas de un actor por ventana. **Cero desactiva la cuota**    |
| `VentanaDeCuotaMinutos`        | 60      | La ventana deslizante del cupo                                   |
| `FallosParaAbrirElBreaker`     | 5       | Fallos seguidos que cortan el paso. Cero desactiva el breaker    |
| `EsperaDelBreakerSegundos`     | 30      | Cuánto espera antes de probar de nuevo                           |
| `RetencionDeRegistrosDias`     | 90      | Cuánto viven las filas de los dos registros                      |
| `PeriodoDePurgaHoras`          | 24      | Cada cuánto corre la purga                                       |

**Un turno que se cae deja fila.** La cuota se cobra en un `finally` —un fallo no puede
ser una forma de consultar gratis—, así que el registro tiene que cobrar en el mismo
lugar: una excepción no prevista escribe una fila con carril y estado `Fallo` y las
llamadas que alcanzó a emitir, y después se relanza. Quien llamó sigue viendo la
excepción; el contrato HTTP no cambia, y de hecho `Fallo` **no tiene nombre en el
contrato**: el mapeo del estado revienta si se le pide uno, que es lo que garantiza que
no se filtre como un quinto estado a los clientes.

**Las dos cotas de tiempo subieron con los modelos que razonan.** El presupuesto del
turno pasó de 30 s a 150 y el timeout por llamada de 20 s a 60: el razonamiento ocurre
**antes** del primer token de la respuesta, así que una generación que antes tardaba
segundos ahora puede tardar decenas. Con los valores viejos el corte llegaba antes que
la respuesta y el turno degradaba como si el proveedor estuviera caído. Son techos, no
esperas: un turno que resuelve rápido no paga nada porque el techo sea alto.

Esta tabla la verifica `OpcionesDocumentadasTests` contra `new OpcionesAsistente()`. Un
default que se mueve sin tocar acá falla el CI, por el mismo criterio con que
`manifiesto-privilegios.json` se compara contra los privilegios efectivos: un valor
operativo documentado es un dato verificado, no prosa.

En desarrollo y en los ambientes efímeros conviene `CupoDeLlamadasPorActor: 0`: el
proveedor es el simulado y no cuesta nada.

### El orden de los decoradores

```
ProveedorConTechoDeLlamadas   ← techo del turno
  └─ ProveedorConBreaker      ← estado del proveedor + timeout por llamada
       └─ proveedor real
```

De afuera hacia adentro, de más barato a más caro. Invertir los dos primeros haría
que el breaker registrara intentos que el techo iba a rechazar igual, y un solo turno
desbocado terminaría abriendo el corte para todos los demás.

**La cuota no está en esta cadena.** La cobra `CapaConversacional` en un `finally`,
con lo que contó `ContadorDeLlamadasDelTurno`: es lo único que conoce al actor, y
meterla acá exigiría un objeto de request mutable con el actor adentro, leído por
capas que no lo declaran.

### Qué sigue funcionando sin proveedor

Cinco de los ocho pasos del pipeline no lo necesitan, así que la falta de modelo **no
corta el turno**. El veredicto se resuelve una vez, antes de empezar, y se consulta
solo en el reescritor y al delegar en el carril SQL.

Con el corte abierto o el cupo agotado: un saludo responde con cero llamadas, una
pregunta ambigua devuelve su menú, y la respuesta a un menú abierto se reconoce. Solo
una pregunta que exige generar una consulta termina en servicio degradado.

## El enrutador en sombra y cómo se lo mide

`EnrutadorDeDominio` corre en **modo sombra**: decide en cada turno y el turno sigue
por el carril SQL igual. No hay a dónde enrutar hasta que existan los edges hacia los
`Contracts` (ARS-46) y los adaptadores de respuesta. Detalle del pipeline y del
catálogo en
[domains/asistente.md](../../../docs/architecture/domains/asistente.md#el-enrutador-y-el-modo-sombra).

Está cableado igual porque ese pedido de aprobación se fundamenta con un número, y el
número no existe si la decisión no se toma nunca. La decisión va a
`asistente.registro_operativo.intencion_sombra`: el nombre de la intención del
catálogo, o nulo si ninguna capturó la pregunta. **Nulo es el caso normal**, no un
dato faltante.

### La cobertura sobre tráfico real

```sql
SELECT count(*) FILTER (WHERE intencion_sombra IS NOT NULL) AS capturados,
       count(*)                                             AS turnos,
       round(100.0 * count(*) FILTER (WHERE intencion_sombra IS NOT NULL)
             / nullif(count(*), 0), 1)                      AS cobertura_pct
  FROM asistente.registro_operativo
 WHERE ocurrido_en >= now() - interval '30 days';
```

Y el desglose, que es lo que dice **cuál** de las cinco intenciones vale la pena
conectar primero:

```sql
SELECT coalesce(intencion_sombra, '(ninguna)') AS intencion,
       count(*)                                AS turnos
  FROM asistente.registro_operativo
 WHERE ocurrido_en >= now() - interval '30 days'
 GROUP BY 1
 ORDER BY turnos DESC, intencion;
```

> **`carril` e `intencion_sombra` no responden la misma pregunta.** `carril` es la
> ruta **real** por la que se resolvió el turno; `intencion_sombra` es la que **se
> habría** tomado. Un turno capturado se resuelve igual por SQL, y también puede
> terminar en `Aclaracion` o en `Fallo` sin dejar de haber sido capturado. Agrupar por
> `carril` para contar capturas devuelve un número, y ese número está mal.

### La tabla dorada: el mismo número sin esperar tráfico

El resolutor es determinista y cuesta cero llamadas al modelo, así que se lo corre
sobre los datasets de evaluación y se obtiene la cota que se puede tener hoy.
[`tabla-dorada-enrutador.json`](../../tests/ArsDocendi.IntegrationTests/Asistente/tabla-dorada-enrutador.json)
fija una entrada por ítem de `capacidad.json` y `robustez.json` con la intención que
captura o nulo, más el bloque `cobertura` con el número —hoy **0 de 39**—.

Mide dos cosas. **Cobertura**: cuántos ítems del corpus captura el catálogo.
**Consistencia de fraseo**: cada ítem de `robustez.json` declara su `origen` en
`capacidad.json`, y como es la misma pregunta dicha de otra manera, el enrutador tiene
que decidir lo mismo para las dos; una divergencia es un error suyo, medible sin
tráfico. Lo que **no** afirma es que la intención capturada sea la correcta para la
pregunta: los datasets llevan `sql_referencia`, no una intención esperada.

**Se regenera a mano y nunca como efecto de correr el test.** Si regenerar fuera
automático, una intención demasiado laxa se absorbería sola en el primer commit que la
causara. Cuando el test falla, dice el ítem y la dirección: `nulo → intención` es
posible laxitud —se revisa la intención, no el dataset— y `intención → nulo` es una
captura perdida. Se edita la entrada, y el diff del archivo es lo que se revisa. Es la
disciplina que [`backend/eval/lineas-de-base/README.md`](../../eval/lineas-de-base/README.md)
ya documenta para el gate de regresión.

**El número offline no es el número del tráfico real**, y no pretende serlo: el corpus
se escribió para medir traducción a SQL, no para parecerse a la demanda. Da la línea de
base contra la que comparar la del tráfico cuando la haya, y el pedido de ARS-46 tiene
que citar las dos diciendo cuál es cuál.

### Qué pasa con la columna cuando ARS-46 se apruebe

**No se borra ni se renombra: pasa a registrar la intención que sí enrutó.** Borrarla
partiría la serie justo en el momento en que se vuelve interesante, porque comparar el
antes con el después es la única forma de saber si la sombra predijo bien. Y un
`RENAME` es un `ALTER`, que el guard del DDL prohíbe, y además rompería toda consulta
escrita contra la serie que la columna existe para preservar.

Corolario aceptado: después del cutover el nombre miente un poco. Lo que se actualiza
es su `COMMENT ON COLUMN`.

## Conexiones

El módulo registra `CadenaSoloLectura` y `CadenaSoloLecturaPii`, derivadas de la
`CadenaDuena` con los roles y contraseñas de la sección `Asistente`. Ver
[data-model.md → Cadenas tipadas](../../../docs/architecture/data-model.md).

Cuál de las dos usa un turno lo decide `IPerfilDelActor`: la de datos personales
exige alcance global **además** del permiso. La política de la aplicación es la
puerta, pero los endpoints de docentes acotan los datos por separado en el
controller; sin la conjunción, el asistente heredaría la puerta sin el acotamiento,
y como `identity.personas` no tiene RLS, un jefe de cátedra leería documento y
teléfono de todo el padrón.

## Evaluación

La métrica primaria del proyecto —corrección con abstención— se mide con el
evaluador de [`backend/eval/`](../../eval/README.md).

Está partido en dos por **qué cuesta dinero**: `ArsDocendi.Evaluacion.Nucleo` está
en la solución y tiene tests en el CI; el ejecutable, que es lo único que
instancia un proveedor real, está fuera, con un guard adentro que falla si vuelve
a entrar.

**Al agregar un ejemplo al catálogo** de este módulo, un test verifica que no
choque con ninguna pregunta del dataset de capacidad. Si chocara, la métrica
mediría cuán bien el sistema reproduce ejemplos que ya vio.

## Dependencias

Solo `ArsDocendi.Shared`. No referencia ningún otro módulo ni su propio
`.Contracts` (que nace vacío: ver
[Modules.Asistente.Contracts/README.md](../Modules.Asistente.Contracts/README.md)).

Tampoco declara EF Core, Npgsql ni MediatR: llegan cuando haya código que los use.

## Schema PostgreSQL

El asistente **lee** los schemas de otros módulos a través de dos roles de solo
lectura, con privilegios enumerados columna por columna, y **escribe** un schema
propio, `asistente`, con sus dos registros. Esos registros son telemetría suya, no
datos del sistema: los escribe la conexión dueña y sus propios roles de lectura los
tienen revocados enteros.

El DDL vive en `database/asistente/*.sql` y se embebe como recurso de **este**
assembly, igual que `database/designaciones/*.sql` en su módulo.
`MigradorAsistente` lo ejecuta en el arranque `--migrate`, **último** de todos los
migradores: los `GRANT` necesitan que las tablas de `identity` y `designaciones`
ya existan.

No usa EF Core. El módulo no tiene entidades de dominio, así que no hay nada que
versionar con un historial de migraciones; los scripts son idempotentes por
construcción y re-ejecutarlos converge.

Los dos archivos corren en orden y el orden importa: `001_asistente_grants.sql`
concede la lectura, y `002_asistente_registros.sql` crea el schema propio y se lo
revoca a los dos roles —para revocar un schema, primero tiene que existir—.

`database/asistente/manifiesto-privilegios.json` es la fuente de verdad de qué se
concede. Un test lo compara contra los privilegios efectivos de la base en tres
direcciones: si alguien agrega una tabla o cambia un `GRANT` sin tocar el
manifiesto, el CI falla.

Los `COMMENT ON` de las tablas y columnas legibles viven en el DDL de **cada
módulo dueño** —`database/identity/013_*.sql` y
`database/designaciones/010_*.sql`—, por el mismo criterio con que las policies RLS
viven en el de `designaciones`. No son documentación: el proveedor de esquema los
lee del catálogo y los pone en el prompt, así que una columna sin comentar le llega
al modelo como un nombre pelado y un tipo.
