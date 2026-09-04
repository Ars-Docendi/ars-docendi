## Why

El cambio anterior —[`asistente-fundaciones`](../asistente-fundaciones/proposal.md)— construyó el sustrato: dos roles de solo lectura sin ningún privilegio de mutación, `GRANT SELECT` enumerado columna por columna contra un manifiesto deny-by-default, cuatro funciones `SECURITY DEFINER` que resuelven el actor, y policies RLS que acotan las cuatro tablas del trámite. También dejó el módulo `Modules.Asistente` registrado en el Host, con su `ping`, la abstracción del proveedor de modelo y las dos cotas de costo por turno.

Lo que **no** construyó es el asistente. Hoy el módulo no traduce ninguna pregunta.

Este cambio construye el **carril SQL**: el camino que toma una pregunta en español, la traduce a una consulta, la valida, la ejecuta acotada al actor y redacta la respuesta. Es donde está el valor del proyecto entero. La familia de casos de uso más importante —cobertura de cátedra y composición del plantel— **no tiene endpoint equivalente en el sistema**: `DesignacionesController` expone hoy únicamente `ping`. No hay carril determinista al que enrutar esas preguntas, así que si el carril SQL no existe, el asistente no responde nada que valga la pena.

El carril tiene exactamente **dos llamadas al modelo** por turno: generación con temperatura cero sobre un prefijo cacheado, y redacción sobre las filas devueltas. Todo lo que va en el medio —validación, envoltura con límite, fijación del actor, guard de resultado— es determinista y cuesta cero tokens. Esa asimetría es deliberada: cada pieza determinista que se agrega al medio es una pieza que no puede alucinar.

Este cambio **no** agrega superficie de usuario: el endpoint `POST /api/asistente/consultas`, el contrato de respuesta con los cuatro estados y el frontend son de la épica E7. Tampoco agrega el enmascaramiento de columnas sensibles (E4) ni la capa conversacional (E5). El carril se construye como un servicio del módulo, ejercitado por tests de integración.

## What Changes

- **Comentarios de esquema en la base**, en español y con sinónimos del dominio, sobre cada tabla y cada columna que el asistente puede leer. Hoy `database/` no tiene un solo `COMMENT ON`. No son documentación: son la capa que le permite al modelo mapear «cuántos titulares tiene Algoritmos» a `designaciones.designaciones`, `designaciones.cargos` e `identity.materias`. Es la pieza de mayor apalancamiento del cambio.
- **Proveedor de esquema** que arma el prefijo estable del prompt de sistema leyendo los privilegios **efectivos** de la conexión —no una lista embebida en el código— junto con esos comentarios, y expone un hash del prefijo completo para el sellado de reportes.
- **Catálogo de ejemplos versionado** de pares pregunta-SQL verificados, más un selector por **similitud léxica** que corre en proceso, sin embeddings ni vector store ni llamadas de red. Los ejemplos viajan en el prompt de **usuario** para no mover el prefijo cacheado.
- **Generación de SQL** con temperatura cero, que devuelve si la pregunta es contestable, la consulta, el razonamiento y una categoría. La **fecha de referencia entra como parámetro del turno** y viaja en el prompt de usuario: la SQL nunca escribe `now()`.
- **Validador de la SQL generada** que tokeniza emitiendo el contenido de los identificadores entrecomillados y lo chequea contra funciones prohibidas, y que rechaza mecánicamente las ocho funciones de reloj.
- **Ejecución envuelta**: conexión y transacción nuevas por ejecución, declaradas `READ ONLY`, con el actor fijado en un ajuste **transaction-local**, timeout de sentencia y de comando, y un límite que pide **una fila de más** para poder distinguir «devolvió N» de «se truncó».
- **Política de abstención** con los siete casos, incluida la distinción entre «no hay datos» y «no podés verlos», y el guard que reconoce tanto cero filas como la fila única de nulos que devuelve una agregación vacía.
- **Redacción en español** sobre las filas, con las reglas de abstención dentro del prompt y sin inventar valores.
- **Orquestador del carril** que compone las siete piezas y devuelve el resultado del turno, todavía sin endpoint.

## Capabilities

### New Capabilities

- `asistente-prefijo-esquema`: prefijo de prompt estable derivado de los privilegios efectivos y de los comentarios de esquema, con hash propio y sin ningún dato variable por turno.
- `asistente-ejemplos-lexicos`: catálogo versionado de pares pregunta-SQL y selector por similitud léxica en proceso, disjunto del dataset de evaluación.
- `asistente-generacion-sql`: primera llamada al modelo, con temperatura cero, fecha de referencia inyectable y corte del turno cuando la pregunta no es contestable.
- `asistente-validador-sql`: segunda capa de defensa sobre la consulta generada, con emisión de identificadores entrecomillados y rechazo mecánico del reloj.
- `asistente-ejecucion-acotada`: ejecución con conexión y transacción nuevas, `READ ONLY`, actor transaction-local, timeouts y detección de truncado por fila sonda.
- `asistente-abstencion`: los siete casos de abstención, con la distinción entre resultado vacío y falta de permiso, y sin declarar nunca cuántas filas quedaron afuera.
- `asistente-redaccion`: segunda llamada al modelo, en español, sujeta a las reglas de abstención y sin errores crudos ni vocabulario de esquema.

### Modified Capabilities

- `persistencia-identity`: las tablas y columnas de `identity` legibles por el asistente reciben comentarios de dominio.
- `persistencia-designaciones`: ídem para las tablas del trámite.

## Impact

- `database/asistente/002_asistente_comentarios.sql` — los `COMMENT ON` de las 14 tablas concedidas y sus columnas.
- `backend/src/Modules.Asistente/Application/` — proveedor de esquema, selector de ejemplos, generador, validador, política de abstención, redactor y el orquestador del carril.
- `backend/src/Modules.Asistente/Infrastructure/` — el ejecutor contra la conexión de solo lectura y la lectura del esquema.
- `backend/src/Modules.Asistente/Recursos/ejemplos-sql.json` — el catálogo versionado, embebido como recurso.
- `backend/src/Modules.Asistente/ModuleExtensions.cs` — registración de las piezas nuevas.
- `backend/tests/ArsDocendi.IntegrationTests/Asistente/` — tests del validador (unitarios, en memoria), del ejecutor y del carril completo contra una base real.
- `docs/architecture/domains/asistente.md` y `docs/architecture/data-model.md` — el carril y los comentarios de esquema.
- **Grafo de dependencias**: sin edges nuevos. `Modules.Asistente` sigue dependiendo únicamente de `ArsDocendi.Shared`. El carril determinista de API, que sí agrega edges hacia `Modules.<X>.Contracts`, es de la épica E6.

## Rollback

Aditivo y reversible por partes:

- Los `COMMENT ON` se revierten con `COMMENT ON ... IS NULL`. Mientras existen no afectan a nadie: son metadatos del catálogo.
- Las piezas del carril son servicios nuevos que hoy no consume ningún endpoint. Quitar su registración del módulo deja el Host arrancando igual.
- No se modifica ninguna tabla, ninguna policy ni ningún `GRANT` del cambio anterior.
