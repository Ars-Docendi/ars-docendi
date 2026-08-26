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

| Variable                       | Default           | Qué es                                   |
| ------------------------------ | ----------------- | ---------------------------------------- |
| `Asistente__Proveedor`         | `simulado`        | `simulado` o `anthropic`                 |
| `Asistente__ClaveDelProveedor` | —                 | La credencial. Nunca al repositorio      |
| `Asistente__Modelo`            | `claude-sonnet-5` | Qué modelo usar                          |
| `Asistente__Esfuerzo`          | `medium`          | `low`, `medium`, `high`, `xhigh` o `max` |

**Por qué esos dos defaults.** El esquema que el modelo maneja es chico —catorce
tablas, poco más de cien columnas— y ése es el factor que más pesa en traducir
preguntas a SQL, así que Opus no se paga. Hacia abajo tampoco conviene: Haiku 4.5
no acepta el parámetro de esfuerzo (cada llamada volvería `400`) y su retiro está
anunciado para no antes de octubre de 2026.

El esfuerzo va en `medium` por costo **y** por un riesgo concreto: los modelos
actuales piensan por defecto, esos tokens se facturan como salida y cuentan contra
el techo de la llamada. La generación de SQL tiene techo de 1200 tokens, así que
con esfuerzo alto el modelo podría gastar el presupuesto pensando y truncar el
JSON — que el generador resuelve como «no pude interpretar la pregunta». Todavía
es una hipótesis; el síntoma serían abstenciones sin explicación sobre preguntas
contestables.

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
| `PresupuestoDelTurnoSegundos`  | 30      | El turno completo, punta a punta (RNF-09). Cero lo deja sin cota |
| `TimeoutDeLlamadaSegundos`     | 20      | Una llamada al proveedor                                         |
| `CupoDeLlamadasPorActor`       | 60      | Llamadas de un actor por ventana. **Cero desactiva la cuota**    |
| `VentanaDeCuotaMinutos`        | 60      | La ventana deslizante del cupo                                   |
| `FallosParaAbrirElBreaker`     | 5       | Fallos seguidos que cortan el paso. Cero desactiva el breaker    |
| `EsperaDelBreakerSegundos`     | 30      | Cuánto espera antes de probar de nuevo                           |
| `RetencionDeRegistrosDias`     | 90      | Cuánto viven las filas de los dos registros                      |
| `PeriodoDePurgaHoras`          | 24      | Cada cuánto corre la purga                                       |

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
