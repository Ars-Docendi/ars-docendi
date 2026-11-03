## Why

El asistente ya responde, ya se acota por permisos y ya conversa. Lo que todavía no tiene es **cota**: nada limita cuánto puede gastar un usuario, nada acota cuánto puede tardar un turno, y cuando el proveedor se cae el módulo devuelve un estado degradado que solo sabe emitir el carril SQL — el resto del sistema, que funciona perfectamente sin modelo, no se entera.

Tres huecos concretos, todos verificables hoy en el código:

1. **No hay cuota por actor.** Con una sola clave de API por ambiente, el proveedor factura al ambiente entero y no puede atribuir consumo a nadie. Si la cuota no vive en la aplicación, no vive en ningún lado.
2. **No hay techo de tiempo.** Hay timeout de sentencia y de comando en la ejecución de SQL, pero **ninguno en las llamadas al modelo**. El peor caso de un turno no tiene cota superior, y los timeouts por etapa se suman: el usuario espera el total, no el máximo.
3. **El modo degradado existe en el enum y casi nadie lo produce.** `CarrilSql` lo emite ante un techo agotado o un proveedor caído; pero el detector de ambigüedad, el reconocedor de aclaraciones y el enrutador social —que resuelven **sin proveedor**— no tienen forma de correr cuando el proveedor está caído, porque el turno muere antes de llegar a ellos.

Y no hay ningún registro: no se sabe cuántos turnos hubo, cuánto costaron, ni qué se preguntó.

**El modo degradado no hay que inventarlo: hay que exponerlo.** Las tres épicas anteriores ya construyeron los carriles deterministas. Lo único que falta es tratarlos como un estado propio en lugar de como un error.

## What Changes

- **Cuota por actor medida en llamadas al modelo**, con ventana deslizante y chequeo **antes** del pipeline. Un turno con reescritor cuesta tres llamadas: contar requests HTTP subestimaría el consumo por un factor de tres. Por identidad autenticada, nunca por dirección de origen — todo el tráfico entra por un túnel y un departamento tras NAT compartiría cupo.
- **Timeout en las llamadas al proveedor** y **techo de tiempo total del turno**, medido punta a punta con un solo `CancellationTokenSource` encadenado. No es la suma de timeouts por etapa: es una única cota sobre lo que el usuario espera.
- **Circuit breaker** con los tres estados clásicos, que abre ante fallos repetidos del proveedor y se recupera solo probando de a una llamada.
- **Resolución degradada del turno**: con el breaker abierto o la cuota agotada, la capa conversacional **sigue corriendo sus pasos deterministas** —enrutador social, reconocedor de aclaración, detector de ambigüedad— y solo se abstiene en el paso que necesitaba el modelo. Un saludo sigue costando cero, y una pregunta ambigua sigue devolviendo su menú.
- **Dos registros desvinculados**, en un schema `asistente` propio: uno operativo con el actor y las métricas, otro analítico con el texto de la pregunta y la fecha **redondeada al día**. Ninguno guarda filas ni SQL, y ninguno lleva `audit.attach` — declarado explícito en la migración, con el motivo escrito.
- **Purga automática** con ventana configurable y test de retención en las dos direcciones.

## Capabilities

### New Capabilities

- `asistente-cuota`: cupo por actor y ventana, medido en llamadas al modelo, chequeado antes de emitir ninguna.
- `asistente-topes-del-turno`: techo de llamadas global del turno y techo de tiempo punta a punta, ambos resolviendo como servicio degradado.
- `asistente-degradacion`: circuit breaker del proveedor y resolución del turno por los carriles deterministas cuando no hay modelo.
- `asistente-registros`: los dos registros desvinculados, su escritura por la conexión dueña y la purga con retención verificada.

### Modified Capabilities

- `asistente-abstencion`: se le suma el caso #6 de la tabla de abstención —proveedor caído o cuota agotada—, que hasta ahora solo sabía emitir el carril SQL. Pasa de ser una salida del carril a ser una decisión tomada **antes** del pipeline, que la capa conversacional respeta paso a paso.

## Impact

- `backend/src/Modules.Asistente/Application/` — la cuota, el reloj del turno, el breaker y su política, y el punto de decisión degradado dentro de la capa conversacional.
- `backend/src/Modules.Asistente/Infrastructure/` — el almacén de la cuota, el decorador del proveedor con timeout y breaker, y el escritor de los dos registros.
- `database/asistente/` — el DDL del schema propio y sus dos tablas.
- `backend/src/Modules.Asistente/ModuleExtensions.cs` — registración y orden de los decoradores del proveedor.
- `backend/tests/ArsDocendi.IntegrationTests/Asistente/` — cuota, breaker y topes en memoria; los registros y la purga contra base real.
- `docs/architecture/domains/asistente.md`, `docs/architecture/data-model.md` y `backend/src/Modules.Asistente/README.md`.
- **Grafo de dependencias**: sin edges nuevos.

## Rollback

Casi todo es aditivo y compone por decoradores. Quitar la registración del breaker y del timeout deja el proveedor desnudo, exactamente como estaba. La cuota y los registros son opt-out por configuración: con la ventana en cero la cuota no bloquea, y sin la migración aplicada el escritor de registros no encuentra tablas — por eso escribe en modo tolerante y nunca hace fallar un turno.

Lo único no reversible por configuración es el schema `asistente` con sus dos tablas, que se revierte con un `DROP SCHEMA` porque no lo referencia nada más.
