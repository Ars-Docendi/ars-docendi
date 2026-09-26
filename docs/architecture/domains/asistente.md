# Domain: asistente

## Propósito

Responder en español preguntas sobre datos que ya viven en la base del sistema, en
modo solo lectura y acotadas al alcance de quien pregunta. Cubre la familia de
casos de uso —cobertura de cátedra, composición del plantel— que hoy **no tiene
endpoint equivalente** en ningún módulo.

## Qué se puede preguntar

**Este catálogo es lo que dimensiona el alcance, y no al revés.** Las tablas que se
le conceden al asistente salen de las preguntas que tiene que contestar, no de las
que la base tiene disponibles. Exponer una tabla de más cuesta para siempre: el
prefijo del prompt viaja en cada llamada.

Dos artefactos consumen este catálogo y **tienen que ser disjuntos**:

| Artefacto             | Qué es                                                               | Dónde                                          |
| --------------------- | -------------------------------------------------------------------- | ---------------------------------------------- |
| Ejemplos verificados  | Pares pregunta-SQL que se le inyectan al modelo por similitud léxica | `Modules.Asistente/Recursos/ejemplos-sql.json` |
| Dataset de evaluación | Los ítems con que se mide la capacidad                               | `backend/eval/datasets/capacidad.json`         |

Si se solapan, la métrica mide qué tan bien copia el ejemplo que ya tiene en el
prompt. La disjunción se decide **acá**, al escribir el catálogo, y no se descubre
después reconciliando dos archivos ya escritos.

### Designaciones — la familia vigente

Cobertura de cátedra y composición del plantel. Dieciséis ejemplos verificados y
veinticuatro ítems medidos, en cuatro categorías: `consulta_simple`, `agregacion`,
`cruce_de_tablas` y `filtro_temporal`.

### Portal docente — la familia propuesta

El caso de uso es **buscar docentes por perfil profesional**, frente a una vacante o
una acreditación CONEAU. Hoy no lo contesta ninguna superficie del sistema: los
dieciocho endpoints de Portal son todos sobre el perfil propio, y el filtro de
`/docentes` sólo cruza apellido, documento, materia, cargo, rol y estado.

Las cuatro que justifican la feature:

| Pregunta                                                   | Categoría         | Tablas                                             |
| ---------------------------------------------------------- | ----------------- | -------------------------------------------------- |
| ¿Qué docentes saben Kubernetes?                            | `cruce_de_tablas` | `habilidades` · `docente_habilidades` · `perfiles` |
| ¿Quiénes tienen posgrado en Ingeniería de Software?        | `cruce_de_tablas` | `educaciones` · `perfiles`                         |
| ¿A quién se le vence una certificación este año?           | `filtro_temporal` | `certificaciones` · `perfiles`                     |
| ¿Qué docentes declararon interés en dictar Bases de Datos? | `cruce_de_tablas` | `docente_habilidades` con `tipo = 'interes'`       |

Alrededor de esas cuatro, la familia se cierra con agregaciones que **no exponen a
nadie** —«¿cuántos docentes cargaron su perfil?», «¿qué habilidades son las más
declaradas?»— y con el cruce hacia designaciones, que es lo que vuelve la respuesta
accionable: «¿qué docentes de Ingeniería de Software declararon experiencia en
industria?».

### Las seis tablas, derivadas de las preguntas

`perfiles` · `habilidades` · `docente_habilidades` · `educaciones` ·
`certificaciones` · `experiencias`

**Ninguna pregunta del catálogo necesita `cvs`, `proyectos` ni
`proyecto_documentos`.** Quedan fuera del alcance hasta que exista una pregunta que
las pida: son tres tablas de prefijo que nadie está usando.

### Lo que NO debe contestar

Estas van al dataset como `no_contestable`, donde **la abstención es la respuesta
correcta**. No alcanza con que la base las rechace: el asistente tiene que decir que
no puede, no fallar.

| Pregunta                                             | Por qué                                                                                                       |
| ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| ¿Cuál es el teléfono personal de X?                  | `portal.contactos` no se concede a ningún rol. El contacto institucional sí, y sale de `identity`             |
| Dame el CV de X                                      | La `uri` del archivo no se concede: el asistente dice que existe y de cuándo es; entregarlo es de la interfaz |
| ¿Qué docentes saben Python? _(actor sin el permiso)_ | La RLS devuelve cero filas. El riesgo no es la fuga: es que el texto afirme «ninguno»                         |

### Contar y enumerar no se pueden separar

`ValidadorDeSql` es lista negra de funciones y palabras clave: **no analiza la forma
del `SELECT`**. «¿Cuántos docentes saben Python?» y «¿quiénes?» pasan por el mismo
camino, con el mismo permiso y la misma policy.

No existe, entonces, el alcance intermedio «que pueda contar pero no listar». Es una
limitación declarada del producto y hay que llevarla así a la conversación con
Secretaría, no dejarla como nota al pie de la implementación.

### Toda respuesta sobre portal declara su cobertura

El portal se llena solo si los docentes lo llenan, y el propio design spec dice que
«el problema del Departamento es que los docentes no cargan nada». Por eso estas dos
son la misma consulta y significan lo contrario:

- «Ningún docente sabe Python»
- «Nadie cargó sus habilidades»

Cada respuesta que se apoye en portal lleva el denominador: _«de 120 docentes, 14
cargaron habilidades»_. Sin eso, el vacío se lee como un hecho sobre las personas.

## Roles que interactúan

Todos los roles del sistema salvo `docente`, según la siembra del permiso
`asistente.consultar`:

- **Jefe de Cátedra** — pregunta por su materia
- **Coordinador de Carrera** — pregunta por su carrera
- **Secretaría Académica** — pregunta por todo el Departamento
- **Decanato** — ídem
- **Administrativo** — ídem, dentro de lo que sus permisos de dominio habilitan
- **Administrador de Sistemas** — el rol existe en la siembra, pero es `NOINHERIT`
  y no hereda permisos: en la práctica no ve nada hasta que se le asigne otro rol

El alcance no lo aplica el módulo: lo aplican las policies RLS con el actor fijado.
El asistente no tiene ninguna rama de código que decida qué puede ver quién.

## Bounded context

- **Pertenece**: la traducción de lenguaje natural a consulta, la validación de esa
  consulta, la política de abstención y la redacción de la respuesta. El catálogo
  de ejemplos pregunta-SQL.
- **No pertenece**: los datos. Todos son de otros bounded contexts —`identity`,
  `designaciones` y `portal`— y el asistente los lee a través de dos roles de
  PostgreSQL con privilegios enumerados columna por columna. No hay ninguna entidad
  canónica acá.

  De `portal` se conceden **seis de las diez tablas**, y su predicado de RLS no
  conjuga el ámbito sino un permiso propio, `portal.ver_trayectoria_ajena`, que nace
  concedido a nadie. El detalle está en [data-model](../data-model.md) y las reglas
  que lo gobiernan en [`business-rules/portal.md`](../../business-rules/portal.md).

## Entidades principales

Ninguna de dominio, y ningún `DbContext`. Lo único que le pertenece son sus dos
registros —`asistente.registro_operativo` y `asistente.registro_analitico`—, que son
telemetría suya y no datos del sistema: nadie más los lee, y los dos roles de solo
lectura del propio asistente tienen ese schema revocado entero.

Lee, con `GRANT SELECT` por columna contra un manifiesto deny-by-default:

| Schema          | Tablas                                                                                         |
| --------------- | ---------------------------------------------------------------------------------------------- |
| `identity`      | `carreras`, `materias`, `personas`, `users`, `roles`, `user_roles`, `permisos`, `rol_permisos` |
| `designaciones` | `cargos`, `periodos`, `pedidos`, `pedido_adjuntos`, `pedido_historial`, `designaciones`        |

Denegadas explícitamente: `designaciones.idempotencia_comandos` (su `response_body`
JSONB guarda respuestas HTTP completas con datos de personas), el schema `audit`
entero, el schema `asistente` entero (sus propios registros), y las columnas
`pedidos.snapshot`, `pedido_adjuntos.uri`, `users.azure_oid` y
`user_roles.granted_by`.

Fuente de verdad: [`database/asistente/manifiesto-privilegios.json`](../../../database/asistente/manifiesto-privilegios.json).

## API pública (contract)

`Modules.Asistente.Contracts` **nace vacío**: ningún otro módulo consume al
asistente. Queda por convención, pendiente de decidir con el equipo si se conserva
o se declara la excepción.

## Endpoints HTTP

| Método | Path                                                   | Permiso                          | Qué                                                                   |
| ------ | ------------------------------------------------------ | -------------------------------- | --------------------------------------------------------------------- |
| GET    | `/api/asistente/ping`                                  | (anónimo)                        | Smoke test, sin base ni proveedor                                     |
| POST   | `/api/asistente/consultas`                             | `asistente.consultar`            | Un turno. Exige `Idempotency-Key`                                     |
| GET    | `/api/asistente/menciones`                             | `asistente.consultar`            | Busca materias o docentes dentro del alcance, para «@»/«#»            |
| GET    | `/api/asistente/capacidades`                           | `asistente.consultar`            | Qué puede hacer el asistente para este actor                          |
| POST   | `/api/asistente/retroalimentacion`                     | `asistente.consultar`            | Califica un turno respondido (thumbs + razón), por posesión del token |
| GET    | `/api/asistente/historial`                             | `asistente.consultar`            | Lista y busca las conversaciones propias                              |
| GET    | `/api/asistente/historial/{id}`                        | `asistente.consultar`            | El detalle de una conversación propia                                 |
| PATCH  | `/api/asistente/historial/{id}`                        | `asistente.consultar`            | Renombra una conversación propia                                      |
| DELETE | `/api/asistente/historial/{id}`                        | `asistente.consultar`            | Borra una conversación propia                                         |
| DELETE | `/api/asistente/historial`                             | `asistente.consultar`            | Borra TODAS las conversaciones propias                                |
| POST   | `/api/asistente/historial/{id}/reanudar`               | `asistente.consultar`            | Reanuda una conversación propia                                       |
| POST   | `/api/asistente/historial/turnos/{id}/reejecutar`      | `asistente.consultar`            | «Volver a consultar» un turno propio ya respondido                    |
| POST   | `/api/asistente/soporte/historial/{actorId}/listar`    | `asistente.leer_historial_ajeno` | Lista el historial de otro actor, con razón obligatoria               |
| POST   | `/api/asistente/soporte/historial/{actorId}/{id}/leer` | `asistente.leer_historial_ajeno` | Lee una conversación de otro actor, con razón obligatoria             |

El ping vive en su **propio controller, sin constructor**. Estuvo junto al turno hasta
que ése ganó dependencias, y ahí se rompió: construir el controller pasó a exigir las
cadenas de solo lectura —cuya fábrica falla si el ambiente no las configuró— y el ping
devolvía 500 sin base. Un ping que necesita configuración de base deja de poder
distinguir «el módulo está cargado» de «la base responde», que es lo único que el
invariante #3 le pide. Hay un guard que lo fija.

### El contrato de respuesta

Cuatro estados, y **el tercero no se colapsa contra el segundo**: «no contestable»
significa que la pregunta no se puede responder nunca; «necesita aclaración» significa
que se puede en cuanto el usuario elija. Colapsarlos hace que el asistente diga «no
puedo» cuando corresponde «¿cuál de estas?».

Por el mismo motivo, `opciones` sólo viaja en el tercer estado: bloquea el turno
esperando una elección. Ningún otro estado lleva un campo equivalente — el asistente
ya no sugiere próximos pasos después de un rechazo o de una respuesta (ARS-140, ARS-149,
design.md D12 de asistente-rediseno-v3). El catálogo de ejemplos verificados sigue
existiendo, pero como la única fuente de ejemplos clicables de la pantalla de bienvenida
(`GET /capacidades`), no como un campo del turno.

### La consulta generada, detrás de un permiso

`asistente.ver_consulta` se siembra y **no se le concede a ningún rol**. No es prudencia
genérica: el `WHERE` de una consulta generada puede llevar un documento, un legajo o un
nombre, así que verla es ver datos que la respuesta redactada no muestra.

Quién necesita eso es una decisión del Departamento y no de quien escribe la migración.
Un permiso concedido de arranque es difícil de quitar; uno vacío se concede en treinta
segundos desde `/membresia-roles` cuando alguien lo pide, y queda registrado quién.

El chequeo vive donde se arma la respuesta y no en el borde HTTP: puesto arriba,
cualquier camino nuevo tendría que acordarse de tapar el campo.

### El catálogo de capacidades

Una caja de texto libre sin descubrimiento es una falsa promesa: el usuario no sabe qué
preguntar, y averiguarlo le cuesta un turno que termina en rechazo.

> **Se deriva de los GRANT efectivos, NUNCA del payload del prompt.**

El esquema se inyecta entero en el prompt, columnas personales incluidas. Un catálogo
derivado de ahí ofrecería preguntas sobre columnas que el rol del usuario no puede leer:
la consulta terminaría en `permission denied`, pero el daño ya está hecho — el catálogo
le habría dicho que esos datos existen y que el asistente los tiene.

Sale de preguntarle a la base «¿qué puedo leer **yo**?», con `has_column_privilege`
contra `current_user`. Los dos roles obtienen catálogos distintos sin que el código sepa
nada de ellos.

**Los ejemplos los valida el motor.** Cada candidato se pasa por `EXPLAIN` con la
conexión del actor: `EXPLAIN` sin `ANALYZE` arranca el ejecutor —y por lo tanto chequea
privilegios— pero no lee ninguna fila. Es más caro que consultar una lista de ejemplos
marcados «seguros», y es lo correcto: una lista se desincroniza del `GRANT` en silencio,
y el modo de falla de esa desincronización es ofrecerle al usuario una pregunta que no
puede hacer.

**El ámbito va aparte de los conteos**: cambia qué filas se ven, no qué se puede
preguntar. Meterlo en los conteos los haría mentir en las dos direcciones.

**La meta-pregunta dejó de tener texto fijo.** «¿Qué podés hacer?» se respondía con un
párrafo escrito a mano que enumeraba cinco áreas sin que nada comprobara que el rol de
quien preguntaba pudiera leerlas. Ahora la responde el catálogo real, y sigue costando
cero tokens.

### Retroalimentación del turno

Un turno `respondida` trae `claveDeRetroalimentacion`: la propia id del turno en
`asistente.registro_analitico`, generada en la aplicación (no por el `DEFAULT` de la
columna) y devuelta una sola vez, en la misma respuesta. Es el único identificador que
cumple «ligado al analítico, nunca al actor» por construcción — cualquier otro exigiría
inventar una clave nueva que, para servir, tendría que apuntar a algo, y lo único
correcto a lo que puede apuntar es exactamente esa fila.

**Autorización por posesión del token, no por identidad.** El analítico no tiene columna
de actor a propósito (TD-012): comparar actores para decidir «solo el autor puede
calificar» reabriría el mismo cruce. En cambio: el token es un UUID aleatorio,
inadivinable, devuelto por HTTPS una sola vez; una ventana de validez de 120 minutos —la
misma que `IAlmacenDeHilos`, en memoria y sin persistencia, igual que `IIdempotencia`—
lo vence; y `POST /api/asistente/retroalimentacion` sigue exigiendo
`asistente.consultar`: el token dice **qué turno**, la policy dice **que sea alguien del
asistente**. Un token vencido y uno inventado devuelven el mismo `404` — para que nadie
pueda distinguir «venció» de «nunca existió» sondeando el endpoint.

**Riesgo residual, aceptado explícitamente**: quien tenga el token —por ejemplo, si se
filtró por un canal ajeno a este diseño, como una pantalla compartida— puede calificar
ese turno. Lo que protege (un voto y una razón sobre una fila ya anónima) no justifica un
esquema más pesado; es el mismo criterio con que el módulo ya acepta el residual de
`ctid` en TD-012.

**Es un upsert, una fila por turno, sin historial.** `INSERT ... ON CONFLICT
(analitico_id) DO UPDATE`: cambiar de voto reemplaza el anterior, nunca lo acumula.
Guardar un historial de razones o de comentarios sería un lugar más donde una queja
rara termina reidentificando a quien la escribió, el mismo argumento que ya vale para
`intencion_sombra`.

**El comentario libre (design.md D7/D14, PO-changed 2026-09-26).** El panel del 👎
acepta, además de cero o más razones de la lista cerrada, un comentario de hasta 500
caracteres, recortado antes de validar el largo y de guardarse (vacío después de
recortar se trata como ausente). Es texto libre junto a una fila anónima —exactamente
el canal de reidentificación que TD-012 existe para acotar—, así que se lo acota en
vez de evitarlo: 500 caracteres, la pista «No incluyas datos personales.» debajo del
campo, la misma purga de 90 días que el resto de la fila, y ninguna pantalla lo
muestra de vuelta (ni siquiera soporte). Ver la adenda de TD-012 en
`docs/quality/tech-debt.md`.

**El logging no puede volver a abrir el cruce, un piso más arriba.** El evento del turno
nombra al actor y nunca el token; el evento del endpoint de retroalimentación nombra al
token y nunca al actor. Sin esa separación, dos líneas de log en el mismo archivo
reconstruirían el join que separar las dos tablas existe para impedir — sin necesitar
ningún acceso a la base.

**Sólo `respondida` califica.** `necesita_aclaracion` no terminó todavía; `no_contestable`
es una abstención correcta y no un fallo que calificar; `servicio_degradado` no es una
respuesta del asistente. Ninguno de los tres trae `claveDeRetroalimentacion`, y el
endpoint responde `404` para cualquier token que se le adivine para esos turnos, porque
nunca se emitió ninguno.

**Sin sugerencias después de una respuesta (ARS-140, ARS-149).** El carril no vuelve a
llamar al catálogo de ejemplos después de `Respondida`: la meta-pregunta («¿qué podés
hacer?») ya lista sus ejemplos ejecutables adentro del propio texto redactado
(`RedaccionDeCapacidades.Texto`), y el resto de los turnos respondidos no ofrece ningún
próximo paso — el único lugar del asistente con ejemplos clicables es la pantalla de
bienvenida, servida por `GET /capacidades` (design.md D12 de asistente-rediseno-v3).

## El carril SQL

Dos llamadas al modelo por turno; todo lo del medio, determinista.

| Pieza                  | Qué hace                                                            | Cuesta |
| ---------------------- | ------------------------------------------------------------------- | ------ |
| `IPerfilDelActor`      | Alcance global y acceso a datos personales; valida el actor         | 0      |
| `IProveedorDeEsquema`  | Prefijo estable del prompt, derivado de los `GRANT` efectivos       | 0      |
| `ISelectorDeEjemplos`  | Ejemplos por similitud léxica, en proceso                           | 0      |
| `GeneradorDeSql`       | **Llamada 1**: temperatura 0, prefijo cacheado                      | 1      |
| `ValidadorDeSql`       | Tokeniza y rechaza funciones y palabras clave prohibidas            | 0      |
| `IEjecutorDeConsulta`  | Transacción nueva `READ ONLY`, actor transaction-local, `LIMIT n+1` | 0      |
| `PoliticaDeAbstencion` | Guard de vacío y decisión de reintento                              | 0      |
| `RedactorDeRespuesta`  | **Llamada 2**: temperatura 0,3, sin caché                           | 1      |

### Cuatro capas de defensa, independientes entre sí

1. **El rol** no tiene ningún privilegio de mutación (`42501`).
2. **La transacción** se declara `READ ONLY` (`25006`).
3. **Las policies RLS** filtran las filas según el actor fijado.
4. **El validador** rechaza la consulta antes de ejecutarla.

Las tres primeras las impone el motor. La cuarta sube el costo de un ataque; el
motor es lo que lo hace inútil. Cada capa tiene su propio test, aislada de las
otras.

Hallazgo del camino: la envoltura en subconsulta hace **estructuralmente**
imposible colar DML, porque PostgreSQL admite una CTE que modifica datos solo en el
nivel superior de la sentencia.

### Menciones «@materia» / «#docente» (design.md D10/D11, ARS-148)

El composer deja elegir una materia o un docente exactos en vez de que el modelo
adivine a partir del texto libre. Dos piezas nuevas, ninguna con privilegio nuevo:

**Búsqueda (`GET /menciones`, `IBuscadorDeMenciones`).** Corre sobre el rol básico
de sólo lectura con `PreambuloDelActor` — transacción `READ ONLY`, actor fijado —,
igual que el resto del módulo. El alcance de las materias lo decide
`identity.asistente_materias_visibles()`; el de los docentes, la RLS de
`designaciones.designaciones` (§ arriba), que ya conjunta `designaciones.ver` con
el ámbito. Un actor sin ese permiso encuentra cero docentes porque la tabla le
queda vacía, no porque el backend lo haya filtrado. Devuelve a lo sumo 6
resultados y un booleano de «hay más» — nunca un conteo, mismo criterio que el
truncado del carril.

**Revisión del manifiesto de sensibilidad (tarea 7.7).** Las columnas que la
búsqueda toca — `identity.materias.name/code`, `identity.carreras.name`,
`identity.personas.nombre/apellido`, `designaciones.cargos.nombre` — están
clasificadas `publica` en `database/asistente/manifiesto-sensibilidad.json`, y
siguen estándolo: **no hizo falta ningún cambio de manifiesto ni de GRANT**. La
única cosa nueva es que este endpoint devuelve el `id` (`identificador`) al
navegador del propio actor, y esa clasificación gobierna otra pregunta —qué sale
hacia el proveedor del modelo—, no qué ve el dueño del dato en su propia pantalla.
El id sigue sin llegar al modelo nunca: ver el punto siguiente.

**Marcadores `$refN` (`GeneradorDeSql`, `ValidadorDeSql`, `EjecutorDeConsulta`).**
El turno manda hasta 5 referencias `{ tipo, id }`; el controller las revalida con
la misma búsqueda antes del candado (una desconocida o fuera de alcance da el
mismo `400` que cualquier otra, sin oráculo de existencia). `ArmarMensaje` agrega
un bloque «Menciones» al prompt de usuario — nunca al prefijo cacheado, así que
`PrefijoDeLosCassettesTests` sigue en pie sin regrabar nada — con el nombre de
cada entidad y un marcador reservado (`$ref1`, `$ref2`…), numerados después de
los que ya trae el segmento. El validador tokeniza `$refN` como su propia clase
de token y rechaza tanto un marcador no declarado como uno declarado que la
consulta ignore — las dos veces, abstención, nunca ejecución contra la entidad
equivocada. El ejecutor reescribe cada `$refN` a un parámetro `@refN` y lo liga
como `uuid`: el id nunca viaja interpolado en un texto que el modelo pueda leer,
ni en éste ni en un seguimiento que edite o anide la misma consulta. Se persiste
en `asistente.turno_historico.referencias` (marcador → tipo e id) para que
«Volver a consultar» y «Reanudar» puedan volver a ligarlo, revalidando contra el
alcance actual en el momento de reusarlo. `DetectorDeAmbiguedad` no pregunta por
un homónimo que una mención de este turno ya nombra.

### La abstención

Siete casos, y uno central: RLS convierte «no tenés permiso» en cero filas, que es
la misma firma que «el literal no matcheó». Antes de gastar el reintento se
consulta si el actor es global; si no lo es, un vacío no lo gasta y la respuesta
dice «no encontré nada en tu alcance», nunca «no hay».

Un resultado vacío se resuelve **sin llamar al modelo**. Con cero filas no hay nada
que narrar, así que la distinción queda garantizada por código y no por una
instrucción del prompt.

Restricción dura: ninguna respuesta declara cuántas filas quedaron afuera. El
indicador de truncado es un booleano y no un número.

### La capa conversacional

Va **encima** del carril, no adentro. Esa separación es lo que deja intactos el
prefijo cacheado, el validador y los datasets: `CarrilSql.ResponderAsync` ya recibía
una pregunta autocontenida, y la capa es quien la calcula.

```
resolver hilo
  └─ enrutador social/meta        ← se SALTEA si hay aclaración pendiente
       └─ reconocedor de aclaración
            └─ detector de cambio de tema
                 └─ reescritor    ← única llamada al modelo de la capa; solo con historial
                      └─ detector de ambigüedad
                           └─ CARRIL SQL
```

Cada posición tiene un motivo:

- **El enrutador social se saltea con un menú abierto.** Si no, un «gracias» le
  robaría la respuesta a la aclaración y el menú quedaría colgado.
- **El reconocedor corre antes del reescritor** y le entrega la etiqueta canónica.
  Si le pasara el «2» que el usuario tipeó, el reescritor tendría que adivinar.
- **El reescritor corre antes del detector de ambigüedad.** «¿y en Análisis
  Matemático?» no contiene ninguna entidad ambigua hasta que se la reescribe.

**El cambio de tema se fuerza, no se pide.** Hay evidencia de modelos que detectan
el pivote y arrastran contexto rancio igual. Al marcarlo, al reescritor **no se le
pasa historial** — no se le pasa historial y una instrucción que diga «ignoralo».
La diferencia es que así el arrastre es imposible por construcción, y el test que lo
verifica no mira la salida del modelo sino qué se le mandó.

**La guarda del marcador anafórico** es lo que evita que «¿y en Sistemas?» se lea
como pivote: ese mensaje menciona un término del catálogo que no está activo, así
que sin la guarda rompería el caso de seguimiento más común que existe.

**La ambigüedad se resuelve con un `SELECT`, y solo con certeza.** Dos clases de
colisión: nombres de materia repetidos entre carreras, y apellidos compartidos. El
índice sale de la base. **No se extiende a la vaguedad**: preguntar tiene un costo
medido, y las aclaraciones de calidad baja son peores que no preguntar.

**El hilo guarda preguntas y nunca filas.** Guardar los resultados devolvería al
prompt, por la puerta del historial, los datos personales que el enmascarador sacó
del camino de salida. No se persiste: se pierde en cada redespliegue y eso está
aceptado.

### La frontera de salida

Los `GRANT` deciden **quién puede leer qué**, y eso lo impone el motor. El
enmascarador decide **qué sale hacia un tercero**, que es una pregunta distinta: un
actor puede tener todo el derecho a ver un teléfono en pantalla y no haber ninguna
razón para que ese teléfono llegue al proveedor del modelo.

Se interpone entre la ejecución de la SQL y la llamada de redacción. Las filas que
llegan al modelo van enmascaradas; las reales siguen viaje al llamador.

`database/asistente/manifiesto-sensibilidad.json` —hermano del de privilegios—
clasifica cada columna legible en tres categorías:

| Categoría        | Qué pasa                                                                |
| ---------------- | ----------------------------------------------------------------------- |
| `publica`        | Viaja al modelo tal cual                                                |
| `sensible-valor` | Al modelo va un marcador estable; el valor real sigue viaje al llamador |
| `sensible-texto` | No viaja en absoluto: se suprime la columna entera, nombre incluido     |

La tercera es para texto libre —`pedido_historial.comentario`,
`pedidos.justificacion`, `pedidos.tipo_baja_detalle`—, donde redactar con reglas es
frágil y no mandarlo es simple: un comentario de rechazo puede nombrar a cualquiera.

**Cómo se identifica la columna.** No por su nombre en el resultado, que es el
alias que eligió la consulta generada: un `SELECT p.documento AS codigo` produce una
columna llamada `codigo`, y comparar nombres la dejaría pasar entera. Se usa el par
`(OID de tabla, número de atributo)` que PostgreSQL emite en la descripción de
filas, que identifica el origen sin importar el alias y sobrevive tanto a la
envoltura en subconsulta del ejecutor como a un `WITH`. El riesgo residual —una
expresión sobre una columna personal deja el par vacío— está en TD-009.

**El marcador es un contador, no un hash del valor.** Un hash de un documento se
invierte por fuerza bruta en segundos, así que viajaría al proveedor y sería el
dato con un paso más. El contador es por orden de primera aparición, lleva la
etiqueta del manifiesto —`«documento 1»`— y no sobrevive al turno.

**Consecuencia de diseño**: con columnas sensibles, la narración deja de ser el
vehículo del dato. El modelo redacta el marco («encontré 4 docentes») y el dato lo
renderiza la interfaz.

**El enmascaramiento es asimétrico.** La pregunta cruda del usuario viaja al
proveedor a través de la generación: si alguien tipea un documento en la pregunta,
llega al modelo igual. Protege el camino de vuelta, no el de ida.

## El carril determinista: catálogo de intenciones

Las preguntas que la API del sistema **ya sabe responder** no necesitan que un modelo reconstruya su consulta. El carril determinista las reconoce contra un catálogo cerrado y las enruta a la API. Hoy está construida **la primera mitad**: el catálogo y la resolución de slots. El enrutador que los consume, y los edges hacia los `Contracts` de los módulos consumidos, son el cambio siguiente.

**Clasificar la intención con el modelo está descartado con evidencia**: 60% de F1 en triage de cinco clases, 77,4% en nueve vías. Un clasificador que falla una de cada cuatro veces, cuesta una llamada y corta el flujo es peor que una tabla.

### Qué declara una intención

`Recursos/intenciones.json` es un archivo, no código disperso. Cada intención declara los términos que tienen que aparecer en la pregunta normalizada, los slots que exige y un **destino lógico**. El destino es un nombre: nadie lo invoca desde el catálogo, y por eso el módulo no gana ninguna referencia nueva.

El reconocimiento es por **conjunto** de términos y no por expresión regular: «¿en qué estado está el pedido de Pérez?» y «¿el pedido de Pérez en qué estado está?» son la misma pregunta, y una regex sobre lenguaje natural es una promesa de precisión que el orden de las palabras no sostiene.

### De dónde salen los valores

Los slots se resuelven contra la base, nunca contra una lista escrita a mano:

| Clase de slot                 | Fuente                                                      |
| ----------------------------- | ----------------------------------------------------------- |
| Materia, Persona              | El índice de entidades que ya usan los dos detectores       |
| Estado, Novedad, Tipo de baja | Los `CHECK` de `designaciones.pedidos`, vía `pg_constraint` |
| Cargo                         | `designaciones.cargos`, por nombre y por abreviatura        |

**Por qué se leen los `CHECK` y no se copian sus valores.** El problema de la lista copiada no es la duplicación sino su modo de fallar: cuando alguien agregue un estado, la lista no rompe nada. El resolutor deja de reconocerlo, la pregunta cae al carril SQL y nadie se entera de que había un camino más barato. Un desajuste que no falla es un desajuste que dura. Por eso una restricción que no enumera literales —o que alguien renombró— es un **error ruidoso** y nunca un vocabulario vacío.

**Y por qué el vocabulario se compone al lado del índice y no adentro.** `CatalogoDeEntidades` lo consumen el detector de ambigüedad, que dispara cuando un término colisiona, y el de cambio de tema, que mide el solapamiento de entidades entre dos preguntas. Meterles «borrador», «Alta» y «Titular» adentro les cambiaría el comportamiento en silencio: palabras que hoy son texto común pasarían a ser entidades del dominio. Componiendo afuera, los dos quedan intactos por construcción y no por un test que lo vigile.

### La colisión no resuelve

Un término que corresponde a más de un valor deja el slot **sin resolver**, y una intención con un slot sin resolver no queda reconocida. Es la misma regla del detector de ambigüedad, escrita en el sentido de este carril: con dos Pérez, enrutar con uno devuelve las filas del otro, y esa respuesta es indistinguible de la correcta para quien preguntó.

**El default es SQL y nunca API.** Enrutar mal hacia la API devuelve cero filas, y «cero filas» es indistinguible de «no hay» — la mentira que la política de abstención prohíbe. Fallar hacia el carril más caro es fallar hacia el que puede responder.

### El enrutador y el modo sombra

El enrutador de dominio corre entre el reescritor y el detector de ambigüedad. Las dos vecindades tienen motivo: después del reescritor porque «¿y el de Pérez?» no tiene slot que resolver hasta que se resuelve la anáfora; antes del detector porque una pregunta que el catálogo cubre con todos sus slots únicos no es ambigua, y hacerla pasar por el menú sería preguntar algo ya decidido.

**Decide y no ejecuta.** Devuelve la intención resuelta o nada; no llama a ninguna API. Nulo es el caso normal y no un error.

**Hoy está en modo sombra**: la decisión se toma sobre tráfico real y se registra, y el turno sigue al carril SQL igual que siempre. No hay a dónde enrutar todavía — faltan los adaptadores de respuesta y los edges hacia `Modules.<X>.Contracts`, que necesitan el acuerdo del equipo por el checklist de cinco pasos.

Está cableado igual porque ese pedido de aprobación se fundamenta con un número —qué proporción del tráfico real captura un catálogo de cinco intenciones y cuántas veces se equivoca— y ese número no existe si la decisión no se toma nunca. No cambia ninguna respuesta, así que no puede romper nada, y se saca borrando unas líneas.

**Dónde queda la decisión.** En `asistente.registro_operativo.intencion_sombra`, con el nombre de la intención o nulo si ninguna capturó la pregunta; nulo es el caso normal. Viaja desde el paso 5 hasta el escritor por un portador con alcance de turno —la misma forma que `ContadorDeLlamadasDelTurno`—, porque el registro se escribe afuera del pipeline, también cuando el turno se cae: una pregunta capturada que después revienta conserva su decisión en la fila del fallo. **No va al registro analítico**, y es una decisión de privacidad tomada explícitamente: las capturas son la minoría, así que cada intención concreta es un valor raro, y un valor raro ahí es el selector que le daría utilidad al canal residual de TD-012.

`carril` **no** cambia de significado: sigue siendo la ruta real del turno. Son dos hechos distintos y ninguno implica al otro — un turno capturado puede terminar en aclaración o en fallo sin dejar de haber sido capturado. La consulta que produce la cobertura sobre tráfico real, con esa advertencia al lado, está en el [README del módulo](../../../backend/src/Modules.Asistente/README.md#el-enrutador-en-sombra-y-cómo-se-lo-mide).

**Cuando ARS-46 se apruebe la columna no se borra ni se renombra**: pasa a registrar la intención que sí enrutó. Borrarla partiría la serie justo cuando se vuelve interesante, porque comparar el antes con el después es la única forma de saber si la sombra predijo bien.

### El corpus ajeno y la tabla dorada

Que el enrutador capture lo que le corresponde lo prueban unos pocos casos. Que **no** capture lo que no le corresponde no se puede probar con casos escritos a mano: uno escribe los que ya sabe que fallan.

Por eso el corpus sale de `capacidad.json` y `robustez.json`. Los escribió otra tarea con otro objetivo —medir traducción a SQL y tolerancia al fraseo—, así que son preguntas legítimas y ajenas al catálogo. Cuando el catálogo crezca, el corpus crece con él sin que nadie lo mantenga, y es lo único que puede fallar por agregar una intención demasiado laxa.

Sobre ese corpus se fija una **tabla dorada** —`backend/tests/ArsDocendi.IntegrationTests/Asistente/tabla-dorada-enrutador.json`, una entrada por ítem con la intención que captura o nulo— y no un assert booleano «ninguna se captura». Los dos fallan en las mismas situaciones, pero piden arreglos opuestos: ante una intención nueva y legítima que **debe** capturar un ítem, el booleano sólo se podía satisfacer debilitando la intención o sacando el ítem del dataset; la tabla se actualiza y el diff muestra la decisión. Y produce un **número** en vez de un veredicto —cuántos ítems del corpus captura el catálogo, hoy 0 de 39—, que es la mitad del dato con que se fundamenta el pedido de los edges y se obtiene sin tráfico real ni una llamada al modelo.

El mensaje de fallo nombra el ítem, la **dirección** del cambio y su lectura: `nulo → intención` es posible laxitud y se revisa la intención, no el dataset; `intención → nulo` es una captura perdida. La tabla **no se regenera** como efecto de correr el test —si lo hiciera, una laxitud se absorbería sola en el commit que la causara— y hereda el guard del banco que reemplaza: otro test verifica que cubra exactamente los ítems de los dos datasets, porque una tabla vacía daría verde para siempre.

**Ya encontró una.** «¿Cuántas solicitudes de baja se presentaron?» caía en `pedidos-de-una-novedad`, porque «solicitudes» normaliza a «pedido» y «baja» es una novedad válida. La intención existe para **listar** los pedidos de una novedad; la pregunta pide **contarlos**, que tiene otra forma de respuesta.

De ahí salieron los **términos excluidos**: una intención puede declarar términos que no deben aparecer. No había alternativa positiva — «¿qué pedidos de Alta hay?» no tiene ninguna palabra de contenido que «¿cuántos pedidos de Alta hay?» no tenga, y lo único que las separa es la presencia de «cuántos».

### Lo que esta pieza no es

**No generaliza, y conviene decirlo.** La guarda que hace viable el enrutador social —interceptar solo si no queda ningún token de contenido— no sirve acá: distinguir «¿cuál es el estado del pedido de Pérez?» de una pregunta arbitraria exige intención **y** slots. Es viable sobre un catálogo chico de preguntas frecuentes.

El catálogo nace con cinco intenciones y crece **de a una, cada una con su caso de prueba**. Hay un test que itera el catálogo y falla nombrando la intención que no tiene caso: es lo que hace barata la disciplina, y la razón de que el catálogo sea un archivo.

**Cuesta cero llamadas al modelo.** Se verifica sobre las dependencias y no contando llamadas: un contador en cero dice que esta vez no llamó; que el tipo no reciba por dónde llamar dice que no puede.

## Presupuesto y degradación

Seis cotas — cuatro persistentes, en Postgres, y dos de proceso — más un estado
propio para cuando alguna se agota (asistente-administracion-de-uso).

### La cuota por actor (persistente, TD-011 actualizado)

Se mide en **turnos por día calendario UTC**, no en llamadas al modelo ni en
una ventana deslizante — eso es lo que cambió: la versión anterior contaba
llamadas en memoria (`CuotaEnMemoria`, ya eliminada) sobre una ventana
deslizante, y se perdía en cada redespliegue. Turnos/día es lo que un admin
no técnico puede razonar y configurar sin conocer el fan-out del pipeline, y
un presupuesto persistido necesita un límite de período estable y auditable.

Con una sola clave de API por ambiente, el proveedor factura al ambiente entero y no
puede atribuir consumo a nadie. Si la cuota no vive en la aplicación, no vive en
ningún lado.

Se acota por **identidad autenticada** y nunca por dirección de origen: todo el
tráfico entra por un túnel, así que un departamento tras NAT compartiría cupo con
sus vecinos.

El cupo efectivo de un actor sale de `asistente.presupuesto_usuario` (un
override vigente, siempre gana, más chico o más grande que el default de su
rol) o, si no hay override, del **mínimo** entre los cupos de
`asistente.presupuesto_rol` de sus roles vigentes que estén **activados**
(mayor que cero) — un rol sin tope (cero) no arrastra a los demás a cero por
ser el número más chico. `0` desactiva el cupo, mismo convenio que el resto
del módulo; los siete roles se siembran en cero hasta que el Departamento
confirme los números reales.

El consumo se **deriva** contando, al vuelo, las filas de
`asistente.registro_operativo` del día con `llamadas_al_modelo > 0` — nunca
un contador propio, para no abrir una segunda fuente de verdad que pueda
desincronizarse del registro real. Un turno cuenta si y solo si invocó al
modelo al menos una vez: un saludo o un menú de aclaración resuelto sin
proveedor no gasta cupo.

El chequeo va **antes** del pipeline: superado el cupo no se emite ninguna llamada,
no una que falle. El cargo (la anotación del turno, que en la implementación
persistente es un no-op porque el consumo ya se derivó del registro) se hace
al terminar el turno, en el mismo `finally` que libera el candado del turno —
así un turno que se cae a la mitad paga igual lo que llegó a gastar.

### El tope organizacional

Un segundo límite, **de toda la organización**, en **USD estimados por mes**
y no en turnos: el cupo por actor responde "¿esta persona usa el asistente
con equidad respecto de sus pares?"; el tope organizacional responde
"¿estamos a punto de recibir una factura inesperada?" — y ahí el driver real
es tokens × precio, no cantidad de turnos, así que dos proveedores o dos
modelos a distinta verbosidad harían de "turnos" una mala proxy.

Se acumula **incrementalmente**, una vez por turno que invocó al modelo (mismo
guard que la cuota), con el precio vigente **en ese momento** —
`asistente.tabla_de_precios`, versionada por rango de vigencia—, contra
`asistente.consumo_organizacional_mensual` (una fila por año/mes; un mes sin
fila vale cero, así que el reset a fin de mes no necesita ninguna acción).
`0` en `asistente.tope_organizacional` desactiva el tope, mismo convenio.

Bloquea a **todos los actores por igual**, incluso a uno que todavía tiene su
propio cupo disponible — es el mismo choque de prioridad que el cupo, un
paso más adelante en `DisponibilidadDelModeloReal`. El texto al usuario
**nunca** menciona el costo ni el tope de la organización: esos números son
del panel de uso, gated por `asistente.administrar`.

Una fila sin ningún precio vigente para su proveedor/modelo **no acumula
nada** — nunca se costea en cero. Es una limitación conocida y no un bug: un
proveedor sin precio cargado no puede aportar al tope, y el panel de uso
sigue reportando esas filas como "sin precio", visibles.

### El turno exclusivo del actor

Un segundo turno del mismo actor mientras el primero está en curso se
rechaza — protege el límite de tasa compartido del proveedor (y, a futuro, el
modelo local de una sola GPU) de dos pestañas, dos dispositivos o un
reintento del cliente llegando a la vez. El guard del lado del cliente
(`useAsistente`, "un turno a la vez") es una cortesía, no un control: no
frena a dos pestañas.

Implementado con un **advisory lock de sesión de Postgres**
(`pg_try_advisory_lock`), no un candado en memoria: un candado de proceso
sólo protege una instancia del Host, y el Host puede correr más de una
(redespliegues rolling). Se toma en una conexión **dedicada, sin pool**
—liberar un candado de sesión exige que la MISMA conexión física llame
`pg_advisory_unlock`, y el pool podría reasignarla con el candado todavía
puesto— y es lo **primero** que se consulta en el turno, antes del cupo, del
tope y del mantenimiento: es el chequeo más barato y el más probable de
rechazar al instante. Se libera en el mismo `finally` que cobra la cuota, en
los cuatro casos (éxito, degradación, excepción, timeout del presupuesto del
turno) y también ante una cancelación del lado del cliente.

Un proceso que muere a mitad de turno libera el candado igual: Postgres lo
suelta en cuanto detecta el socket caído. Sólo un bug de código que se salte
el `finally` mientras el proceso sigue vivo podría dejarlo colgado — el mismo
modo de falla que cualquier otro `finally` olvidado.

### El modo mantenimiento (kill switch)

Un interruptor persistido y auditado (`asistente.modo_mantenimiento`, fila
única) que un admin prende o apaga con una razón — obligatoria para prender,
no para apagar. `GET /api/asistente/capacidades` lo reporta siempre, sin
caché de proceso: cada consulta relee la fila, para que dos instancias del
Host vean el mismo valor sin ningún mecanismo de invalidación.

Detrás de un puerto chico (`IDisponibilidadDelModulo`), a propósito: hoy lo
implementa Postgres, y nada de Azure se referencia en ningún lado —el puerto
existe para que un futuro backend en Azure App Configuration sea una clase
nueva y un cambio de composición, nunca un cambio en quien lo llama, mismo
patrón que el módulo ya usa para el proveedor del modelo.

Un admin con `asistente.administrar` puede seguir usando el asistente en
mantenimiento — para verificar la recuperación antes de reabrirlo a todos —,
pero el bypass es **sólo del motivo mantenimiento**: sigue sujeto a su propio
cupo, al tope organizacional y a la exclusión de turno concurrente. La
decisión de bypass la toma el LLAMADOR (`CapaConversacional`, que resuelve
los permisos del actor), nunca el puerto de almacenamiento del flag.

### Las dos cotas de tiempo

| Cota                                 | Dónde                                            |
| ------------------------------------ | ------------------------------------------------ |
| Timeout de una llamada al proveedor  | `ProveedorConBreaker`                            |
| Presupuesto total del turno (RNF-09) | `PresupuestoDelTurno`, en la capa conversacional |

**No son la misma cosa y la segunda no se deriva de la primera.** Cuatro llamadas de
diez segundos son cuarenta segundos de espera y cada una habría respetado su límite.
El presupuesto es un único `CancellationTokenSource` encadenado al token del
request, creado al entrar a la capa y propagado hacia abajo.

Las etapas conservan sus propios timeouts —el de sentencia libera el backend de la
base, cosa que cancelar un token no hace—, pero ninguno de ellos es la cota del
turno.

`PresupuestoDelTurno.Vencio` distingue «se acabó el tiempo» de «el usuario cerró la
pestaña». Sin esa distinción, cada abandono se registraría como una caída del
servicio.

### El circuit breaker

Tres estados —cerrado, abierto, en prueba—, con el estado en el proceso y no en el
request. Cuenta **fallos de transporte y de timeout**, nunca rechazos semánticos: un
modelo que devuelve una respuesta que el validador descarta está sano, y cortarle las
llamadas por eso apagaría el asistente cada vez que alguien pregunta algo difícil.

En prueba deja pasar **una sola** llamada, no una por turno: con varios turnos
concurrentes, «una por turno» sería una avalancha contra un proveedor que recién se
levanta.

El proveedor se envuelve de afuera hacia adentro, de más barato a más caro:

```
ProveedorConTechoDeLlamadas   ← techo del turno
  └─ ProveedorConBreaker      ← estado del proveedor + timeout por llamada
       └─ proveedor real
```

### El grabador de cassettes va por fuera del reintento

Debajo de esos tres decoradores está el **cliente HTTP con nombre** del módulo,
que es otro pipeline y se arma en el orden inverso al que se lee:

```
adaptador  →  grabador de cassettes  →  reintento de transporte  →  transporte
```

El grabador intercepta el **cable**, no al adaptador: ve cuerpos HTTP y no tipos
del SDK, así que el guard que fija el SDK en un solo archivo sigue en pie sin
excepción nueva. Graba el cuerpo **crudo** de la respuesta, y ahí está el punto:
un decorador de `IProveedorDeModelo` habría sido más fácil de escribir y habría
grabado la respuesta **ya traducida**, dejando el parseo del adaptador —la mitad
que ningún test cubría— del lado de afuera del cassette.

**Por qué por fuera y no por dentro del reintento.** Del lado de adentro grabaría
también los fallos, con ellos el 429 y el 503 verdaderos del proveedor. Se
descarta por tres cosas concretas:

1. **Rompe la identidad del cassette.** Los cuatro campos de la clave —prefijo,
   mensaje, esfuerzo, modelo— son idénticos en los tres intentos. Distinguirlos
   exigiría meter el número de intento adentro de la clave, que es estado del
   transporte y no de la pregunta.
2. **Reproducir un fallo reproduce la espera.** El reintento haría su backoff de
   verdad al replay, y una suite que duerme por un cassette es una suite que
   alguien va a apagar.
3. **No cubre nada nuevo.** `ReintentoYTechoTests` ya ejercita el reintento contra
   el cable, `retry-after` incluido. Lo que no estaba cubierto es el parseo de una
   respuesta **exitosa** real, y esa es la que este orden graba.

Costo asumido: nunca vamos a tener un cassette de un 429 real. Si hace falta, se
escribe a mano, que es lo que los tests del reintento ya hacen.

**Apagado por default y falla cerrado.** Con `Asistente__DirectorioDeCassettes`
vacío el handler **ni se registra** y el pipeline queda idéntico al de antes.
Configurado y sin `Asistente__RegrabarCassettes`, una llamada sin cassette lanza
**sin invocar hacia adentro**: es lo que hace de «nunca una llamada de red en CI»
una propiedad del código y no una promesa. Detalle operativo en
[el README del módulo](../../../backend/src/Modules.Asistente/README.md).

### El modo degradado no se inventa: se expone

Cinco de los ocho pasos del pipeline no necesitan proveedor. La falta de modelo **no
corta el turno**: la capa conversacional resuelve el veredicto una vez, antes de
empezar, y lo consulta solo donde hace falta.

| Con el modelo caído, sin cupo, sin tope o en mantenimiento | Qué pasa                       |
| ---------------------------------------------------------- | ------------------------------ |
| Un saludo o un agradecimiento                              | Responde, cero llamadas        |
| Una pregunta con entidad ambigua                           | Devuelve su menú de aclaración |
| La respuesta a un menú abierto                             | Se reconoce y se cierra        |
| Una pregunta de seguimiento                                | Se responde sin reescribir     |
| Una pregunta que exige generar SQL                         | Servicio degradado             |

El texto distingue las causas. Con la cuota agotada el sistema **sabe** cuándo
vuelve el cupo y lo dice; con el proveedor caído no lo sabe nadie y no promete plazo;
con el tope organizacional agotado, nunca menciona costo ni tope; con el
mantenimiento activo, nombra la razón que el admin escribió al activarlo.

**`MotivoSinModelo` tiene hoy seis valores** (`Ninguno` no cuenta, es "se puede
llamar"): `CuotaAgotada`, `ProveedorCaido`, y los tres que suma
asistente-administracion-de-uso — `TopeOrganizacionalAgotado`,
`TurnoConcurrente`, `Mantenimiento`. **Los seis siguen resolviendo como el
mismo `EstadoDelTurno.ServicioDegradado`** del contrato HTTP: el "carril es un
servicio... antes de tener los cuatro estados" es un invariante deliberado
(ver "Decisiones registradas" más abajo), y cada motivo nuevo es,
semánticamente, "no se puede conseguir una respuesta del modelo ahora" —
exactamente lo que ese estado ya significa para el cliente. `TurnoConcurrente`
es la única excepción de ORIGEN: no pasa por `DisponibilidadDelModelo` — el
candado del turno se consulta antes de siquiera resolver el hilo, así que un
segundo turno concurrente nunca llega a correr ninguno de los ocho pasos del
pipeline, ni los que no necesitan proveedor.

## Los dos registros

Dos tablas en el schema `asistente` que **no se cruzan**, con retención de 90 días y
purga automática.

| Registro             | Guarda                                                                                     | No guarda                                |
| -------------------- | ------------------------------------------------------------------------------------------ | ---------------------------------------- |
| `registro_operativo` | actor, momento, carril, estado, llamadas, tokens, latencia, reintento, truncado, proveedor | El texto de la pregunta, y la credencial |
| `registro_analitico` | pregunta, categoría, estado, **fecha redondeada al día**                                   | El actor, la hora exacta                 |

La columna `proveedor` guarda la identidad que expone el puerto —`anthropic/claude-sonnet-5`—, nunca la clave. Es lo que permite separar el costo de antes y el de después de un cambio de modelo, que sin ella quedarían mezclados en la misma serie. Va solo al registro operativo: en el analítico sería una dimensión más por la cual agrupar preguntas, y a esta escala cada dimensión achica el conjunto en el que un usuario se esconde.

**Ninguno guarda las filas devueltas ni la consulta generada.** Ni por defecto ni
detrás de un flag: son exactamente los datos que el enmascaramiento acaba de sacar
del camino de salida, y un `WHERE` puede llevar un documento. No están en el tipo
que recibe el escritor, así que no se pueden persistir por accidente.

**Por qué la fecha va redondeada**: con alrededor de treinta usuarios, un timestamp
preciso en las dos tablas permitiría reidentificar al autor de cada pregunta con un
join por tiempo. Desvincular sin quitar la hora no desvincula nada.

**Por qué el analítico no tiene clave secuencial**: una identidad autoincremental
sería, ella misma, la clave del join —la fila _n_ de una y la fila _n_ de la otra
serían el mismo turno—. Usa un UUID aleatorio. Queda un residual: el orden físico de
las filas todavía correlaciona. Está declarado, y es TD-012.

**Sin `audit.attach`, y declarado explícito en la migración.** Todas las tablas del
repositorio lo llaman al final de su archivo; acá no, porque `audit.change_log`
guarda la fila entera en JSON y no tiene política de retención: el texto de cada
pregunta sobreviviría a la purga en otro lado. La ausencia es una decisión, y hay un
test que falla si a alguna de las dos tablas le aparece el disparador.

**Los escribe la conexión dueña.** Los dos roles de solo lectura tienen el schema
`asistente` revocado entero: un asistente que pudiera consultar el registro analítico
respondería «qué le preguntó fulano al asistente» a cualquiera con el permiso de
consulta.

**El registro nunca hace fallar un turno.** Un registro que rompe el turno que estaba
registrando convierte la observabilidad en una fuente de indisponibilidad. Es la
decisión inversa a la del enmascarador, y a propósito: ahí un fallo silencioso filtra
datos, acá un fallo ruidoso niega un servicio que funciona.

## La superficie de usuario

`frontend/src/features/asistente/`, con **un solo montaje**: un lanzador en la
barra superior que abre la conversación en un **modal centrado** —`PanelAsistente`—.
Hasta `asistente-rediseno-v3` (ARS-140/ARS-151, tasks.md §10) había una segunda
implementación, una ruta `/asistente` a página completa; se borró porque dos
implementaciones se desincronizan, y porque una ruta a la que hay que navegar no
resuelve el descubrimiento: si el usuario tiene que acordarse de que el asistente
existe y buscar dónde está, no lo usa. Es un modal y no un cajón lateral porque la
conversación es la tarea mientras dura: un cajón compite por el ancho con una
pantalla que quedó atrás y a la que nadie está mirando.

`/asistente` sigue existiendo, pero sólo como redirect: lleva a la home (`/portal`)
con la marca `?asistente=abrir` en la URL, que el lanzador consume —con el permiso
abre el modal y borra la marca; sin él sólo la borra— (design.md D8 de
asistente-rediseno-v3). Un vínculo viejo nunca queda en una pantalla muerta.

**El lanzador elimina un fake UI.** En la barra superior había un botón «Ayuda»
`disabled` con `title="Próximamente"`, que es exactamente lo que el invariante #7
prohíbe. Activarlo con el asistente quita superficie falsa en lugar de agregar
superficie nueva.

### El acceso se pregunta, no se deduce del rol

El frontend consulta `GET /api/asistente/capacidades`: si responde, hay acceso; si
responde `403`, no.

Lo tentador sería una lista de roles —«todos menos Docente»— y es el mismo antipatrón
que el backend rechazó al sembrar el permiso: `identity.roles` **no es un catálogo
cerrado**, Secretaría puede crear roles desde la aplicación, y una lista embebida
falla **abierta** con cualquiera que no conozca. El fallo no daría error: le mostraría
el asistente a alguien que no debería verlo.

El catálogo que trae de vuelta es el mismo que la vista necesita para su pantalla
inicial, así que la consulta no es un costo extra. Y sin acceso no hay formulario:
sin el permiso, el lanzador no pinta nada —ni el botón, ni una pantalla muerta—,
así que no hay ningún campo con botón que rechace al enviar.

### La conversación vive en el dueño del montaje

El estado de la conversación —turnos, hilo, turno en vuelo— lo crea `useAsistente`
en quien monta la vista: el lanzador de la barra. `PanelAsistente` lo recibe por
prop y no tiene conversación propia.

No es una prolijidad: el panel se monta al abrir el modal y se desmonta al cerrarlo,
y Esc o un clic afuera —también sin querer— cierran. Con el hilo en el panel, un
clic fuera tiraba la conversación entera y el turno en vuelo con ella. El lanzador
vive con la barra, así que al reabrir la conversación sigue donde estaba, y un turno
que estaba en vuelo al cerrar llega igual y espera.

Nada se guarda en el navegador: las filas traen datos personales, y persistirlas en
`localStorage` sin política de retención contradice lo que el enmascarador acaba de
proteger. La conversación muere al recargar, como decidió el backend al no
persistir el hilo.

### Un turno a la vez, y el cliente nunca queda colgado

Mientras hay un turno en vuelo no se envía otro —ni por Enter, ni por el botón, ni
por un chip—, aunque se puede seguir escribiendo. Dos pedidos concurrentes son dos
claves de idempotencia, es decir dos cobros, y el segundo sale con el hilo viejo o
nulo y abre una conversación que nadie pidió. El guard vive en el hook, no sólo en
la vista, para que ningún montaje futuro lo pierda.

Cada turno viaja con un `AbortSignal` y un timeout **por request** de 160 s, apenas
sobre el presupuesto de 150 s del turno en el backend: el que corta tiene que ser el
servidor, con su mensaje de degradado, y el cliente es sólo la red de seguridad. El
timeout no va en el cliente HTTP compartido porque el resto de la aplicación no
tiene turnos de 150 s.

Un `404` de hilo perdido —el backend los expira por inactividad— descarta el
identificador del lado del cliente: la siguiente pregunta abre una conversación
nueva en lugar de repetir el mismo error.

### Reintentar reusa la clave, y sólo sobre un turno que terminó en error

Un turno que falló ofrece «Reintentar», que reenvía el mismo texto con la misma
`Idempotency-Key`: es el uso documentado de la clave, y si el backend ya había
terminado cuando se cortó la conexión, devuelve lo que guardó sin volver a llamar al
modelo.

**El límite sale de leer el backend.** `IdempotenciaEnMemoria` consulta la caché
**antes** de ejecutar el turno y guarda **después**, sin registrar el turno en
curso: un segundo pedido con la misma clave mientras el original sigue corriendo
ejecuta el turno entero otra vez, y el último `Guardar` pisa al primero. Por eso el
botón existe únicamente en turnos con error —red, 5xx, 404, timeout del cliente—,
que llegan cuando el request ya terminó, y **nunca** en vuelo ni sobre un turno
que el usuario dejó de esperar.

### «Dejar de esperar» dice exactamente lo que hace

Mientras hay un turno en vuelo aparece «Dejar de esperar», con el mismo umbral que
el indicador. Aborta el request del cliente y libera el campo, y eso es todo lo que
hace: el backend no se entera, sigue el turno hasta el final y cobra la cuota. El
turno queda con «Dejaste de esperar la respuesta. La consulta ya salió y cuenta para
tu cupo.», como nota y no como error —lo pidió el usuario—, y sin «Reintentar», por
el límite de arriba.

No se llama «Detener» ni «Cancelar» porque ninguno de los dos es cierto, y ni el
nombre ni ningún tooltip insinúan que se ahorró la llamada al modelo.

### Conversación nueva

«Nueva conversación» vacía el hilo y descarta el identificador; el backend acepta
un hilo nulo como conversación nueva, así que es real. Sin confirmación, porque no
hay nada persistido que perder. Deshabilitado sin turnos y en vuelo.

### El rail de conversaciones (`asistente-rediseno-v3`, ARS-142)

`RailDeConversaciones` reemplaza al cajón `ListaDeConversaciones`/`AbrirHistorial`
previo: vive **siempre montado** a la izquierda del modal (268 px expandido, 60 px
colapsado, con la misma transición de grilla del modal) en vez de abrirse a pedido,
porque el historial persistido (`GET /historial`) es información permanente, no un
estado transitorio de la conversación en vuelo. La preferencia de
expandido/colapsado se guarda en `localStorage` **por usuario**
(`asistente.rail.<userId>`, con `try/catch`: un storage que tira sigue permitiendo
alternar, sólo no recuerda la próxima vez) — nunca los turnos, por el mismo motivo
que el resto del feature no persiste nada en el navegador.

Cada turno respondido invalida `["asistente","historial"]` y usa
`RespuestaDelAsistente.Conversacion` (D13) para resaltar la fila activa y titular
el encabezado: el rail nunca adivina cuál conversación está viva, se lo dice la
propia respuesta del turno. La búsqueda del rail (`GET /historial?q=`) se mantiene
aunque el mock de referencia no la dibuje, porque las archivadas tienen que seguir
siendo encontrables por texto.

### Archivar y borrado diferido, desde el rail (ARS-143/ARS-144)

El «⋮» de cada fila abre `Archivar`/`Desarchivar` (activas) o `Desarchivar` (ninguna
fila archivada ofrece «Archivar» dos veces) y `Eliminar`, con el mismo criterio de
404-idéntico del backend para una fila ajena o inexistente. Archivar o eliminar la
conversación **activa** vacía el hilo a la pantalla de bienvenida — no queda un
panel mostrando una conversación que el rail ya no lista —, y «Deshacer» la
reanuda (`POST .../reanudar`) en vez de sólo destacarla de nuevo.

**Eliminar nunca es un `DELETE` inmediato del lado del cliente tampoco.** Un
`DELETE` (uno o «Borrar todas») devuelve `{ loteDeBorrado }`, la fila desaparece
del rail al instante (el backend ya la excluye de `GET /historial`), y
`AvisoDeDeshacer` — al pie del rail, visible incluso colapsado, superpuesto al
hilo — ofrece «Deshacer» durante los 10 s que la interfaz cuenta (el servidor da 15:
10 de aviso más 5 de margen de red, `Asistente__VentanaDeDeshacerSegundos`). Un solo
aviso a la vez: una acción nueva reemplaza al anterior en el mismo componente en vez
de apilarlos. Pasados esos 10 s sin deshacer, el borrado es lógicamente definitivo
del lado del cliente (ya lo era del lado del backend desde el `DELETE`); dentro del
minuto siguiente (`Asistente__PeriodoDeBarridoDeBorradosSegundos`) el
`BackgroundService` del servidor lo hace físico —ver «Historial de conversaciones»
más abajo—, sin que el cliente tenga que seguir abierto ni pedir nada más. «Borrar
todas» conserva su confirmación inline (única acción del historial que la pide,
porque afecta todo de una vez) y su copy ahora nombra las archivadas y el margen de
10 s; «Eliminar» de una sola fila no pide confirmación, porque tiene deshacer.

El anuncio de cada acción (archivar, eliminar, deshacer, vencimiento) sale por la
región viva **existente** del hilo (`Conversacion`'s `anuncio`, un `<li>` oculto
visualmente con la clase `adoc-sr` dentro del `role="log"` de siempre) y no por el
propio `AvisoDeDeshacer`, que es `role="presentation"`: el recuadro visible y el
anuncio a lectores de pantalla son dos superficies separadas a propósito, para que
el mismo texto no se anuncie dos veces por dos regiones vivas —un test cuenta
`aria-live` en la página y falla si aparece una segunda—. El foco va a «Deshacer»
al aparecer el aviso, a la fila restaurada al deshacer, y a la lista si el aviso
vence (o se resuelve) con el foco todavía en el botón.

### Editar y reenviar la última pregunta (ARS-147, design.md D9)

Sólo la última pregunta de la conversación —vigente o restaurada por «Reanudar»—
ofrece «Editar y reenviar»: un textarea inline reemplaza la pregunta, la respuesta
queda al 40 % de opacidad mientras se edita, y Escape cancela devolviendo el foco al
botón. Reenviar manda un `POST /consultas` normal con `reemplaza` (la
`Idempotency-Key` del turno en vivo que se reemplaza, o el `id` de
`turno_historico` de uno restaurado) y **no** con un endpoint propio: cupo,
candado e idempotencia tratan el reemplazo como un turno cualquiera, cobrado una
sola vez.

El backend resuelve la nueva pregunta contra la **misma foto de contexto** que
tenía la reemplazada —segmento vigente y aclaración pendiente snapshoteados antes
de ese turno, guardados en el propio `TurnoDelHilo`— y sólo pisa el turno viejo
(hilo en memoria y fila de `turno_historico`, en una sola transacción) si la
respuesta nueva llega a un resultado registrable; un `Reemplaza` que no nombra el
último turno vigente del hilo, o un hilo vencido, da `409` sin tocar nada. El
`claveDeRetroalimentacion` viejo se revoca (rechaza como un token desconocido en
adelante) y el título de la conversación **no cambia** — el historial conserva
sólo la versión final, sin contador de versiones ni rastro de la pregunta
reemplazada. La interfaz no dibuja «N / M»: la navegación de versiones del mock de
referencia se descartó por decisión de producto (design.md, «Decisiones… pendientes
de confirmación del PO»).

### Barra de acciones y motivos del 👎 (ARS-146)

`BarraDeAcciones` reemplaza a los botones de texto de `AccionesDelMensaje`/
`VotoDeRetroalimentacion` por íconos con nombre accesible y tooltip: «Copiar
respuesta», y con tabla «Ampliar tabla»/«Exportar a CSV», más 👍/👎 con
`aria-pressed`. Visible siempre en el último turno y en cualquiera ya votado;
en los demás aparece por hover o **foco dentro del turno** (nunca sólo hover, para
que el teclado la alcance), y siempre en el orden de tabulación aunque no se vea.
El panel del 👎 («¿Qué falló? Opcional») es de elección **múltiple** —«Datos
incorrectos», «No entendió la pregunta», «Faltan datos», «Otro», pastillas
independientes con `aria-pressed`, más un comentario libre acotado a 500
caracteres con contador y la pista «No incluyas datos personales.»—, igual que el
mock de referencia (PO-changed 2026-09-26; ver design.md D7/D14 y la
adenda de TD-012 en `docs/quality/tech-debt.md`). El motivo retirado `lento` no
aparece más en la interfaz ni en la API —se retiró del todo, no quedó como legado,
porque nada shippeó a producción con esa razón (ver «Retroalimentación del
turno»)—.

### Orden de la tabla y vista ampliada (ARS-145)

El orden de columnas es estado puramente del cliente (`ordenarFilas`, `numérico` /
`fecha ISO` / colación española, vacíos siempre al final, `stable sort` sobre el
índice original) y vive en el turno, no en `TablaDeResultado`: reabrir un turno
viejo no hereda el orden de otro. Los vínculos de celda (`Vinculos[]`) siguen la
fila **por su índice original**, no por su posición visual, así que ordenar nunca
rompe a qué trámite abre un clic. «Tabla ampliada» superpone el resultado sobre todo
el modal con «Copiar tabla»/«Exportar a CSV» en el **orden mostrado** —nunca el
orden en que la respuesta llegó—; Escape la contrae sin cerrar el modal encima, y
el foco vuelve a «Ampliar tabla».

### Menciones «@materia» / «#docente», del lado del cliente (ARS-148)

`PopoverDeMenciones` es un combobox ARIA (`aria-activedescendant`) que sólo busca a
partir de 2 letras (antes, un aviso fijo, cero pedidos al backend); cada resultado
elegido queda como chip en el composer y viaja en `referencias` mientras su texto
siga en la pregunta —borrar el `@materia`/`#docente` del texto quita también la
referencia, para que nunca se manden ids que ya no corresponden a nada visible—.
Escape cierra el popover, nunca el modal (mismo orden de escapes que el menú «⋮» y
la tabla ampliada).

**Límite conocido: un turno reanudado o leído del historial muestra sus menciones
como texto plano, sin chip.** `TurnoDeHistorialDto` (`GET /historial/{id}`,
«Reanudar») expone `{ id, pregunta, sql, estado, ocurrioEn }` — la pregunta ya
incluye el texto «@Análisis Matemático» tal como se escribió, pero no la
estructura `{ tipo, id }` que un chip necesita para ser interactivo; esa
estructura vive sólo en `turno_historico.referencias`, del lado del servidor, para
revalidar «Volver a consultar»/«Reanudar» (ver abajo), no para redibujar la
interfaz. Editar y reenviar una pregunta reanudada reenvía su texto tal cual, sin
poder tocar sus menciones originales como chips.

**Revalidación de menciones heredadas.** Cada mención que un turno reutiliza
—«Volver a consultar» sobre un turno propio, o el primer turno de una conversación
recién reanudada— se revalida contra el alcance **actual** del actor con la misma
búsqueda que `GET /menciones`, nunca contra el alcance que tenía cuando se hizo la
pregunta originalmente: un permiso o un ámbito que se achicó desde entonces hace
que la re-ejecución se abstenga con el mismo texto amigable que cualquier SQL que
ya no corre, nunca un error crudo ni una ejecución contra la entidad equivocada.

### La accesibilidad, que es donde estaba el defecto conocido

| Regla                                                          | Por qué                                                                                                                                                                                                                                                            |
| -------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `role="log"` + `aria-live` **solo sobre la lista de mensajes** | En el prototipo previo la región envolvía el contenedor entero, así que cada re-render hacía que el lector leyera todo de nuevo                                                                                                                                    |
| La línea de métricas, **fuera**                                | Cambia en cada turno: adentro es lo que más ruido genera                                                                                                                                                                                                           |
| El indicador, en su propio `role="status"` y fuera del log     | Es un estado, no un mensaje de la conversación                                                                                                                                                                                                                     |
| Umbral de aparición del indicador                              | Los tres carriles se diferencian en un orden de magnitud; un indicador que parpadea es peor que ninguno                                                                                                                                                            |
| Foco al campo de entrada al responder                          | Quien usa teclado o lector no tiene que volver a buscarlo                                                                                                                                                                                                          |
| Foco de vuelta al lanzador al cerrar el modal                  | Sin eso el foco se perdía en `body`, y quien navega por teclado tenía que volver a encontrar la barra                                                                                                                                                              |
| `inert` sobre `#root` mientras el modal está abierto           | El `Modal` de la librería no contiene el foco: Tab se escapaba hacia la página de atrás. Se portalea a `body`, hermano de `#root`, así que hacer inerte la raíz contiene el foco sin un trap a mano. Es un workaround (TD-014) hasta que el `Modal` traiga el suyo |
| El razonamiento, como `<details>` cerrado dentro del mensaje   | Es parte de la respuesta y va en la región viva; el contenido de una disclosure cerrada no se anuncia hasta abrirla, así que no le agrega ruido al lector                                                                                                          |
| La columna sensible, anunciada                                 | Un candado sólo visual no le dice nada a quien no lo ve: el candado va `aria-hidden` y la cabecera lleva «(dato personal)» sólo para lectores                                                                                                                      |

**No se inventan etapas.** «Interpretando… consultando… redactando…» exigiría streaming
del servidor, que cambia el contrato; un progreso simulado por temporizador sería el
mismo fake UI que este cambio vino a eliminar.

### Lo que se muestra, y lo que no

Los cuatro estados se renderizan distinguibles, y el **degradado va como aviso, no como
error**: un banner rojo le diría al usuario que hizo algo mal, y su pregunta no tiene
nada de malo.

`opciones` sólo se presenta en la aclaración: continúa el turno esperando una elección.
Ningún otro estado ofrece preguntas nuevas para probar — desde ARS-149 el único lugar
con ejemplos clicables es la pantalla de bienvenida.

**El razonamiento se lee a pedido, y sólo en modo debug.** El backend lo redacta
para el usuario final y lo sigue mandando en toda respuesta —queda visible en
devtools/network, es una decisión aceptada—, pero el cliente sólo lo RENDERIZA como
disclosure cerrada, «Cómo lo interpreté», al lado de «Ver la consulta», cuando la
variable de build `VITE_ASISTENTE_DEBUG` vale `"true"` (asistente-razonamiento-solo-en-debug).
Apagado por defecto, y sin fallback a `import.meta.env.DEV`: a diferencia de
`VITE_DEVELOPMENT_AUTH_ENABLED`, correr `vite dev` no lo prende solo. El resolver
puro vive en `frontend/src/features/asistente/utils/modoDebug.ts`. La pregunta
interpretada queda **visible** como «Entendí: …», fuera de la disclosure y sin
depender del modo debug: es el aviso de que la pregunta se reinterpretó, y
esconderlo derrotaría su razón de ser.

**Ninguna etiqueta interna llega a la pantalla.** `metricas.categoria` —el carril
que resolvió el turno— salió del tipo del cliente: el backend la sigue mandando y lo
que no está en el tipo no se puede pintar por descuido. `cubre[].nombre` es
`schema.tabla` y tampoco se muestra, ni `cubre[].descripcion`: es el comentario de
la tabla en PostgreSQL, el mismo texto que va al modelo en el prefijo del prompt,
con nombres de tablas y advertencias para el modelo. De las áreas del catálogo
sólo se dice cuántas hay, en la línea del alcance.

Las columnas sensibles se renderizan como tabla con los **valores reales** —nunca
viajaron al modelo— y **se marcan**: candado en la cabecera y una leyenda bajo la
tabla, «Las columnas con candado contienen datos personales.». Se dice qué es
personal, no por dónde viajó ni cómo se enmascaró: eso es mecánica interna. El
truncado se avisa **sin números**: «ves 3 de 124» es un canal de inferencia sobre
datos que el usuario no puede ver.

**Sólo acciones reales por mensaje.** «Copiar respuesta» y, con tabla, «Copiar
tabla»/«Exportar a CSV», únicamente cuando el portapapeles del navegador existe;
👍/👎 (calificar) y, en la última pregunta, «Editar y reenviar», los dos con
backend propio — ver «Barra de acciones y motivos del 👎» y «Editar y reenviar la
última pregunta» más arriba. **Regenerar y adjuntar siguen sin backend** y por
eso siguen sin botón: uno que no hiciera nada sería el fake UI que el invariante
#7 prohíbe.

## Historial de conversaciones y acceso de soporte

**El hilo en memoria (`HiloConversacional`) siempre fue "guarda preguntas y nunca
filas" — este feature persiste exactamente eso, sin ampliar el invariante.**
Cada turno de cada conversación se escribe a `asistente.hilo_historico`/
`asistente.turno_historico`: la pregunta interpretada, la SQL que respondió
(cuando hubo una), el estado del turno y los dos momentos. Nunca las filas
que devolvió una consulta, nunca el texto redactado de la respuesta.

**No hay opt-out por conversación, y es una decisión de producto, no un
descuido.** El sistema tiene que poder responder "el usuario X preguntó Y" —
a través del historial propio del usuario, y a través del acceso de soporte
auditado — y un modo que se pudiera saltear contradiría eso directamente. La
única exclusión es `Fallo`: un turno que revienta con una excepción no
prevista nunca produjo un cuerpo HTTP (el mapeo del contrato revienta si se
le pide un nombre público para ese estado), así que no hay nada coherente
que mostrar en una conversación retomada para él.

**El punto de escritura es el mismo que ya escribía los dos registros
anónimos**: `CapaConversacional.RegistrarAsync`, el único lugar que ve las
tres salidas del turno (éxito, timeout, excepción) con el actor, el mensaje y
un reloj ya en la mano. `IRegistroDeHistorial` nunca hace fallar el turno —
mismo criterio que `IRegistroDelTurno` — porque el turno ya se resolvió y el
usuario ya tiene su respuesta.

**`hilo_historico.id` es independiente del id efímero del hilo en memoria.**
El hilo en memoria vive 120 minutos y se pierde en cada redespliegue por
diseño; la conversación persistida tiene que sobrevivir 180 días. El puente
entre los dos es `HiloConversacional.HiloHistorico` (`Guid?`, nulo hasta el
primer turno que se persiste), fijado por el escritor y leído por
`Reanudar`.

### Auto-título, búsqueda, archivar y las operaciones propias

El título de una conversación se deriva de su primera pregunta la primera
vez que se persiste (truncado a 80 caracteres en un límite de palabra, con
elipsis) y nunca se vuelve a tocar solo — renombrarla es explícito y
permanente, y un reemplazo (D9) tampoco lo cambia. La búsqueda usa
`to_tsvector('spanish', pregunta)` con un índice GIN, no un `ILIKE`: con
acentuación y flexión española, `ILIKE` ni usa índice ni entiende que
"designación" y "designaciones" son la misma raíz.

**Archivar** (`hilo_historico.archivada_en`, asistente-rediseno-v3, design.md
D3) es un timestamp nulable que no toca `ultima_actividad` — la retención de
180 días de abajo sigue contando igual — y que `RegistroDeHistorial` limpia
solo al escribir un turno nuevo: una conversación archivada retomada se
desarchiva sola. `GET /historial` sigue listándola, marcada
(`archivada: true`), en vez de escondiéndola en un endpoint aparte.

**Borrar ya no es un `DELETE` inmediato (asistente-rediseno-v3, design.md
D4).** `DELETE /historial/{id}` (una) y `DELETE /historial` (todas, archivadas
incluidas) marcan `borrado_pendiente_desde`/`lote_de_borrado` y devuelven el
lote — la fila sigue existiendo. Toda consulta propia (`IConsultasDeHistorial`)
filtra `borrado_pendiente_desde IS NULL`, así que la conversación desaparece de
inmediato de las cuatro operaciones propias (ver, renombrar, reanudar, y de la
propia lista) para su dueño, sin que el `DELETE` físico haya ocurrido todavía.
`POST /historial/borrados/{lote}/deshacer` limpia esas dos columnas si el lote
es propio y sigue dentro de `Asistente__VentanaDeDeshacerSegundos` (default 15
s); un lote ajeno, desconocido o vencido da el mismo `404` — tres casos
indistinguibles a propósito, mismo criterio que un id que no existe o es de
otro actor en las demás operaciones. Pasada la ventana el borrado es
**lógicamente** definitivo (nadie que lo consulte por los canales propios lo
va a volver a ver) y se vuelve **físicamente** definitivo — sin papelera, sin
recuperación, con cascada sobre sus turnos — dentro del minuto siguiente,
cuando `BarridoDePendientes.BarrerAsync` corre (ver «Retención y purga» más
abajo): la finalidad no depende de que el cliente siga abierto.

### Reanudar: siembra, no revive

`POST /historial/{id}/reanudar` no intenta reactivar el viejo id efímero —en
general no se puede, porque puede llevar meses vencido. En cambio,
`IAlmacenDeHilos.Sembrar(actor, hiloHistorico, turnos)` crea un hilo en
memoria **nuevo**, con la vigencia normal de 120 minutos, ya cargado con los
turnos persistidos vía el mismo `Agregar` que usa cualquier turno en vivo, y
con `HiloHistorico` ya fijado en la conversación persistida. Los turnos
siguientes en ese hilo nuevo extienden la misma conversación en lugar de
abrir una. El seguimiento con anáfora ("¿y el de Pérez?") funciona contra el
contexto reanudado exactamente igual que si la conversación nunca se hubiera
cortado — es la misma mecánica de `HistorialVigente` de siempre.

### «Volver a consultar»: tabla, nunca un segundo redactado

Un turno propio `respondida` ofrece re-ejecutar su SQL guardada, bajo el
alcance **actual** del actor — no el que tenía cuando preguntó. Es
deliberadamente tabla-solamente y no un segundo llamado al modelo: la SQL es
el artefacto durable (es lo único que este feature persiste de la
"respuesta"), el texto redactado no lo es. Reusa `IEjecutorDeConsulta` tal
cual —mismas tres capas de RLS, mismo tope de filas, mismo timeout de
sentencia— así que no hay una segunda ruta de enmascarado que mantener
sincronizada con la primera. Un rechazo del motor (privilegio que se achicó,
esquema que cambió) resuelve como una respuesta amigable y no técnica, nunca
un error crudo — el mismo criterio que ya usa el carril en vivo para
`ConsultaSinPrivilegio`/`ConsultaRechazadaPorElMotor`.

No escribe una fila nueva de `turno_historico` (no es una pregunta nueva) ni
de `registro_operativo`/`registro_analitico` (no hubo modelo): contarla ahí
inflaría las métricas de uso con una acción que no las gastó.

### Acceso de soporte: permiso propio, razón obligatoria, auditoría que no se puede apagar

`asistente.leer_historial_ajeno` sigue el mismo patrón que
`asistente.ver_consulta` — sembrado, y a **ningún** rol, ni siquiera
`sys_admin` — pero resuelve una pregunta distinta: no "¿puedo ver la SQL de
MI pregunta?" sino "¿puedo leer las conversaciones de OTRA persona?". Tener
`asistente.consultar` no alcanza; los dos permisos se comprueban por
separado y ninguno implica al otro.

Los dos endpoints de soporte son `POST`, nunca `GET`, para que la razón
obligatoria viaje en el cuerpo y jamás en una URL —donde terminaría en un
log de acceso, un proxy, o el historial del navegador—. Cada llamada escribe
una fila en `asistente.auditoria_acceso_historial` **antes** de devolver
cualquier dato; si esa escritura falla, no sale ningún dato — al revés de la
disciplina de `IRegistroDelTurno`/`IRegistroDeHistorial`, y a propósito: acá
la garantía es "se audita antes de leer", y tragarse el fallo de esa
escritura la convertiría en "se audita salvo que falle", que es justo el
acceso sin auditar que este capability existe para impedir.

`auditoria_acceso_historial.hilo_historico_id` **no lleva clave foránea**: la
fila tiene que sobrevivir a que el propio dueño borre esa conversación (que
puede, en cualquier momento, con las cuatro operaciones propias de arriba).
Sin FK, esa fila sigue siendo perfectamente legible después del borrado —
quién leyó, a quién, cuándo, por qué — que es exactamente lo que un rastro de
auditoría tiene que garantizar.

La lectura de soporte muestra la SQL **siempre**, sin exigir además
`asistente.ver_consulta`: es un solo gate deliberado, porque quien ya está
confiado con leer el historial entero de otra persona no gana nada de
protección real por un segundo permiso sobre la SQL sola — sólo friction
para el camino de soporte legítimo. Nunca ofrece filas de resultado ni una
acción de re-ejecución: es texto y momentos, nada más, y ningún endpoint del
módulo — ni el propio, ni el de soporte — expone al sujeto si, cuándo o
quién leyó su historial. Es la decisión final del cliente (no una pendiente):
el mismo trade-off que ChatGPT Enterprise Compliance API, Claude Enterprise
audit logs y Microsoft Purview eDiscovery ya asumen — el log de acceso queda
del lado de quien administra, nunca visible para el sujeto.

### Retención y purga

180 días para el historial propio (`Asistente__RetencionDeHistorialDias`),
contados desde `ultima_actividad` de la conversación y no desde su creación
— más largo que los 90 días de los registros anónimos, porque retomar una
conversación de hace meses tiene que seguir funcionando. 365 días para la
auditoría de soporte (`Asistente__RetencionDeAuditoriaDeSoporteDias`), en
una ventana **independiente** de la del historial que describe —
justamente porque tiene que sobrevivirlo. `PurgaDeRegistros` suma estos dos
barridos a los dos que ya tenía; mismo mecanismo, mismo `TimeProvider`, mismo
criterio de loguear y seguir si una vuelta falla. **Las conversaciones
archivadas siguen la misma retención de 180 días** que las demás: archivar no
mueve `ultima_actividad`, así que archivar una conversación no la protege de
la purga ni la expone antes que a cualquier otra.

**El barrido de un minuto, aparte de la purga diaria (asistente-rediseno-v3,
design.md D4).** La finalidad lógica de un borrado ya la da el filtro de
lectura de arriba en cuanto vence `Asistente__VentanaDeDeshacerSegundos`; la
física la da `BarridoDePendientes.BarrerAsync`, corrida por el
`BackgroundService` `BarridoDeBorradosPendientes` cada
`Asistente__PeriodoDeBarridoDeBorradosSegundos` (default 60 s) — scoped, mismo
patrón que `PurgaDeRegistros`, con su propio `TimeProvider` inyectable para
test. `PurgaDeRegistros` corre la misma sentencia como red diaria, para el
despliegue donde el barrido corto no llegó a levantar. Un tick que falla se
loguea y no frena al siguiente — mismo criterio que el resto de la purga.

### TD-012, con una dependencia nueva y documentada

Nada de este feature toca el mecanismo de desvinculación de
`registro_analitico`: sigue sin actor, sin timestamp preciso, sin FK hacia el
operativo, y el historial nunca lo referencia. Lo que cambia es que ahora
existe, en el mismo schema, una tabla que responde a propósito la pregunta
que el analítico se niega a responder — pero atribuida a su dueño, y a un
lector de soporte permisionado y auditado. La garantía de anonimidad de
TD-012 sigue valiendo frente al asistente mismo (que no puede leer ninguna
de las dos tablas) y frente a cualquiera sin acceso al historial; deja de
valer, por diseño, frente a quien tiene el historial — el dueño, o soporte
con permiso, razón y auditoría.

## Reglas de negocio (BR-\*)

Ninguna propia. El asistente no decide nada del dominio: expone lo que otros
módulos ya decidieron. Las reglas que lo acotan son de seguridad, no de negocio, y
viven en el manifiesto de privilegios y en las policies RLS.

## Dependencias

- **Hacia adentro**: solo `ArsDocendi.Shared` (cadenas tipadas, permisos, migración
  de módulo). Ningún edge hacia otro módulo: el carril determinista de API, que sí
  agrega edges hacia `Modules.<X>.Contracts`, es de la épica E6.
- **Hacia afuera**: nadie lo consume.
- **Externas**: un proveedor de modelo de lenguaje, detrás de `IProveedorDeModelo`.
  Dos implementaciones: `ProveedorSimulado` (default de todos los ambientes) y
  `ProveedorAnthropic`, que se elige con `Asistente__Proveedor=anthropic` y exige
  clave. El SDK del proveedor se nombra en un solo archivo, fijado por un test de
  arquitectura, así que sumar un adaptador —o cambiar de proveedor— es una clase y
  un brazo del `switch`. **El asistente no accede a ninguna otra fuente externa**:
  opera exclusivamente sobre la base del propio sistema.

## Specs activas

- `openspec/changes/asistente-fundaciones/` — roles, manifiesto, permiso, funciones
  del actor, RLS, cadenas tipadas y módulo base
- `openspec/changes/asistente-carril-sql/` — este carril
- `openspec/changes/asistente-evaluacion/` — el eje de capacidad y la exclusión del CI
- `openspec/changes/asistente-enmascaramiento/` — el manifiesto de sensibilidad y la frontera de salida
- `openspec/changes/asistente-capa-conversacional/` — el hilo, lo social, la aclaración y el seguimiento
- `openspec/changes/asistente-presupuesto-degradacion/` — cuota, topes, breaker y los dos registros
- `openspec/changes/asistente-superficie-api/` — el contrato de respuesta, los endpoints y el catálogo de capacidades
- `openspec/changes/asistente-frontend/` — la feature, sus dos montajes y la accesibilidad
- `openspec/changes/asistente-proveedor-anthropic/` — el primer adaptador real del puerto
- `openspec/changes/asistente-rediseno-conversacion/` — la conversación: reintento con la misma clave, «Dejar de esperar», conversación nueva y que sobrevive al cierre del modal, foco, columnas sensibles marcadas y el aspecto con tokens del tema
- `openspec/changes/asistente-catalogo-de-intenciones/` — el catálogo cerrado y el vocabulario del trámite
- `openspec/changes/asistente-enrutador-de-dominio/` — la decisión de carril en modo sombra
- `openspec/changes/asistente-registro-de-la-decision-sombra/` — la decisión al registro operativo y la tabla dorada del corpus
- `openspec/changes/asistente-feedback-export-seguimiento/` — el token de retroalimentación, la exportación CSV y las sugerencias de seguimiento
- `openspec/changes/asistente-historial-conversaciones/` — el historial propio, reanudar, «volver a consultar» y el acceso de soporte auditado
- `openspec/changes/asistente-rediseno-v3/` — un solo montaje, el rail, archivar, borrado diferido con deshacer, editar y reenviar la última pregunta, menciones «@materia»/«#docente» y el fin de las sugerencias fuera de la bienvenida

## Evaluación

La métrica primaria del proyecto es **corrección con abstención**, y se mide con el
evaluador de [`backend/eval/`](../../../backend/eval/README.md).

Está partido en dos por **qué cuesta dinero**, no por qué es «de evaluación»:
`ArsDocendi.Evaluacion.Nucleo` —generador del fixture, dataset, puntuación,
preflight, reporte— está en la solución y tiene tests en el CI;
`ArsDocendi.Evaluacion` —el ejecutable, lo único que instancia un proveedor real—
está **fuera**, con un guard adentro que falla si vuelve a entrar. El CI corre los
tests de la solución sin filtro, y el síntoma de olvidarlo sería una factura, no un
test rojo.

Hoy no se puede correr, pero por la otra mitad: el adaptador real existe
(`ProveedorAnthropic`, `Asistente__Proveedor=anthropic`) y lo que falta es una clave
y presupuesto aprobado. Sigue registrado como TD-008, y la corrida en sí la bloquea
**ARS-67**.

Esa corrida deja además, si se la configura para eso, los **cassettes** del
proveedor: el mecanismo ya está y probado sin clave, pero los cassettes de salida
real llegan con ella y **no son parte de este trabajo**. Lo que la fixture congelada
no detecta —un cambio de formato de cable del proveedor— está registrado como
TD-017.

## Decisiones registradas

- **El esquema del prompt se deriva de los privilegios efectivos** — una lista
  embebida en código se desincroniza en silencio y falla en las dos direcciones:
  describe columnas revocadas y omite columnas nuevas.
- **Los `COMMENT ON` viven en el DDL de cada módulo dueño** — mismo criterio con
  que las policies RLS viven en el de `designaciones`.
- **Similitud léxica y no embeddings** — con decenas de ejemplos, un vector store
  es un servicio, un modelo y una llamada de red más por turno para elegir entre
  pocas opciones.
- **La fecha de referencia es un parámetro del turno** — hace el eval determinista
  y permite prohibir el reloj entero en el validador sin romper ningún caso.
- **El límite pide una fila de más** — sin la fila sonda, «devolvió N» y «se
  recortó» son indistinguibles y la redacción afirma totales falsos.
- **El actor va transaction-local** — uno de sesión sobreviviría al pool y un turno
  heredaría el actor del anterior, respondiendo con el alcance equivocado sin
  tirar error.
- **El carril es un servicio y no un endpoint** — construir el contrato antes de
  tener los cuatro estados y el hilo conversacional obligaría a inventarlo dos
  veces.
- **Las menciones se buscan con el rol básico, nunca con la conexión dueña** —
  el mismo motivo que el resto del módulo: si el filtro de alcance viviera en
  C#, alguien podría olvidarlo sin que nada avise (design.md D10 de
  asistente-rediseno-v3).
- **El id de una mención viaja al navegador pero nunca al modelo** — un
  identificador estable y pseudónimo de una persona es exactamente el canal que
  TD-022 y la clasificación `identificador` quieren cerrado; el marcador `$refN`
  es la frontera (design.md D11).
- **Manifiesto de sensibilidad sin cambios** — la búsqueda de menciones sólo lee
  columnas ya `publica`; devolver el `id` al dueño de la sesión es una pregunta
  distinta de la que ese manifiesto gobierna (design.md D10, tarea 7.7).
