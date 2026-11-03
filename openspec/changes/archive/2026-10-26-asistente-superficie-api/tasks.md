## 1. Contrato de respuesta (ARS-47)

- [x] 1.1 `ResultadoDelTurno` — campo `Sugerencias`, separado de `Opciones`
- [x] 1.2 `ResultadoDelTurno` — campo `Sql`, nulo salvo permiso
- [x] 1.3 `Application/Sugerencias.cs` — elige del catálogo verificado, por parecido léxico, con respaldo
- [x] 1.4 `CarrilSql` — todo rechazo pasa por el armador de sugerencias
- [x] 1.5 `CapaConversacional` — la aclaración agotada también sugiere
- [x] 1.6 Test: una aclaración trae opciones y no sugerencias
- [x] 1.7 Test: un rechazo trae sugerencias y no opciones
- [x] 1.8 Test: cada sugerencia es la pregunta de un ejemplo del catálogo
- [x] 1.9 Test: sin parecido léxico igual hay sugerencias
- [x] 1.10 Test: ninguna respuesta contiene el nombre de una tabla ni un código de error

## 2. El permiso de ver la consulta (ARS-47, segunda mitad)

- [x] 2.1 `Permisos.AsistenteVerConsulta` = `asistente.ver_consulta`
- [x] 2.2 `database/identity/014_identity_permiso_ver_consulta.sql` — siembra sin conceder a ningún rol, con el motivo escrito
- [x] 2.3 Migración EF que lo aplica, con su `Down`
- [x] 2.4 `IPerfilDelActor` — resolver si el actor ve la consulta
- [x] 2.5 `CarrilSql` — incluir la consulta solo con el permiso
- [x] 2.6 Test: sin el permiso la consulta no viaja
- [x] 2.7 Test: con el permiso la consulta viaja y es la que se ejecutó
- [x] 2.8 Test: recién migrada, ningún rol tiene el permiso
- [x] 2.9 Verificar en rojo: devolver la consulta siempre hace fallar 2.6

## 3. Catálogo de capacidades (ARS-49)

- [x] 3.1 `Application/CapacidadesDelActor.cs` — el modelo del catálogo
- [x] 3.2 `Infrastructure/CatalogoDeCapacidades.cs` — deriva áreas y conteos de `LectorDeCatalogo`
- [x] 3.3 Validar cada ejemplo candidato con `EXPLAIN` sobre la conexión del actor
- [x] 3.4 Elegir de cuatro a seis, diversificando por categoría, de forma determinista
- [x] 3.5 Los límites —no escribe, no sale del sistema, no ve lo que tu rol no ve— declarados con su motivo
- [x] 3.6 El ámbito del actor va aparte de los conteos
- [x] 3.7 Cachear por rol: los `GRANT` no cambian en runtime
- [x] 3.8 Test: el catálogo de un actor sin datos personales no menciona ninguna columna personal
- [x] 3.9 Test: dos actores con acceso distinto reciben conteos distintos
- [x] 3.10 Test: los conteos coinciden con lo que el rol puede leer
- [x] 3.11 Test: un ejemplo que toca una columna personal no se ofrece al rol básico
- [x] 3.12 Test: cada ejemplo ofrecido sale del catálogo verificado
- [x] 3.13 Test: el ámbito no altera los conteos
- [x] 3.14 Test: pedir el catálogo cuesta cero llamadas al modelo
- [x] 3.15 Test: con el corte al proveedor abierto, el catálogo responde igual
- [x] 3.16 Verificar en rojo: armar el catálogo siempre con la conexión de datos personales. **El primer intento no dio rojo**: el test miraba el TEXTO redactado buscando «documento», y la redacción nunca nombra columnas. Se lo reescribió contra los conteos de `identity.personas` e `identity.users`, y ahí sí discrimina

## 4. La meta-pregunta deja de tener texto fijo (ARS-49, cierre)

- [x] 4.1 `CapaConversacional` — la intención meta se resuelve con el catálogo
- [x] 4.2 Redacción del catálogo en español, sin etiquetas internas
- [x] 4.3 Test: «¿qué podés hacer?» menciona áreas y ejemplos reales
- [x] 4.4 Test: la meta-pregunta sigue costando cero llamadas al modelo

## 5. El endpoint del turno (ARS-48)

- [x] 5.1 `Api/ModelosAsistente.cs` — el pedido y la respuesta HTTP
- [x] 5.2 `AsistenteController` — `POST /api/asistente/consultas`, con el permiso del asistente
- [x] 5.3 `AsistenteController` — `GET /api/asistente/capacidades`
- [x] 5.4 `Infrastructure/IdempotenciaEnMemoria.cs` — clave acotada por actor, con expiración
- [x] 5.5 Rechazar el pedido sin clave con un mensaje que la nombre
- [x] 5.6 `OpcionesAsistente` — vigencia de la clave de idempotencia
- [x] 5.7 Resolver el actor de la identidad de la sesión, nunca del cuerpo del pedido
- [x] 5.8 Test: sin permiso, 403; sin identidad, 401
- [x] 5.9 Test: sin clave, 400 con el nombre de la cabecera
- [x] 5.10 Test: la misma clave devuelve la misma respuesta y no llama al proveedor
- [x] 5.11 Test: la clave de un actor no le sirve a otro
- [x] 5.12 Test: al expirar, el turno se vuelve a procesar
- [x] 5.13 Test: el actor sale de la sesión y no del cuerpo del pedido
- [x] 5.14 Verificar en rojo: sin acotar la clave por actor, 5.11 falla

## 6. Documentación

- [x] 6.1 `docs/architecture/api-contracts.md` — los dos endpoints nuevos
- [x] 6.2 `docs/architecture/domains/asistente.md` — el contrato, el catálogo y el permiso
- [x] 6.3 `backend/src/Modules.Asistente/README.md` — la superficie HTTP
- [x] 6.4 `docs/architecture/data-model.md` — el permiso nuevo

## 7. El ping, que se rompió al agregarle dependencias al controller

- [x] 7.1 `Api/PingAsistenteController.cs` — controller propio, **sin constructor**
- [x] 7.2 Sacar el ping de `AsistenteController`
- [x] 7.3 Guard: el controller del ping no tiene ninguna dependencia
- [x] 7.4 Guard: el ping no volvió al controller del turno
- [x] 7.5 El enrutador social ya no responde la meta-pregunta con texto fijo, y sus tests lo fijan
