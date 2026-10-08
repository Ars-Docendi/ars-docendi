## 1. Cuota por actor (ARS-52)

- [x] 1.1 `Application/ICuotaDelActor.cs` — contrato: consultar cupo y anotar consumo
- [x] 1.2 `Infrastructure/CuotaEnMemoria.cs` — ventana deslizante por actor, con reloj inyectado
- [x] 1.3 El cargo lo hace `CapaConversacional` en un `finally`, con lo que contó `ContadorDeLlamadasDelTurno`
- [x] 1.4 `OpcionesAsistente` — cupo de llamadas y ventana; cupo en cero desactiva la cuota
- [x] 1.5 El chequeo va **antes** del pipeline, en `CapaConversacional`
- [x] 1.6 Texto de cuota agotada en `PoliticaDeAbstencion`, sin etiquetas internas
- [x] 1.7 Test: un turno con reescritor anota tres llamadas
- [x] 1.8 Test: un saludo no consume cupo
- [x] 1.9 Test: los reintentos de transporte no suman al cupo
- [x] 1.10 Test: con el cupo agotado **el proveedor no recibe ninguna llamada**
- [x] 1.11 Test: dos actores no comparten cupo
- [x] 1.12 Test: al pasar la ventana vuelve el cupo
- [x] 1.13 Test: cupo en cero nunca bloquea
- [x] 1.14 Verificar en rojo: una ventana en balde fijo en vez de deslizante hace fallar el test de la ventana

## 2. Topes del turno (ARS-53)

- [x] 2.1 `Infrastructure/ProveedorConTimeout.cs` — timeout por llamada al proveedor
- [x] 2.2 `Application/PresupuestoDelTurno.cs` — un solo `CancellationTokenSource` encadenado al token del request
- [x] 2.3 `CapaConversacional` crea el presupuesto y propaga su token a todo el pipeline
- [x] 2.4 `OpcionesAsistente` — presupuesto total del turno (RNF-09: 30 s) y timeout por llamada
- [x] 2.5 Distinguir «venció el presupuesto» de «el usuario canceló» mirando el token del request
- [x] 2.6 Al vencer, resolver como servicio degradado y no como cancelación cruda
- [x] 2.7 Test: varias etapas lentas agotan el presupuesto aunque ninguna supere el suyo
- [x] 2.8 Test: el presupuesto vencido devuelve servicio degradado
- [x] 2.9 Test: la cancelación del usuario **no** se registra como degradación
- [x] 2.10 Test: el techo de llamadas sigue siendo global del turno
- [x] 2.11 Test: el resultado reporta el consumo real de llamadas
- [x] 2.12 Verificar en rojo: quitar el encadenado hace que 2.7 no corte

## 3. Circuit breaker (ARS-54, primera mitad)

- [x] 3.1 `Application/EstadoDelBreaker.cs` — cerrado, abierto, en prueba
- [x] 3.2 `Infrastructure/BreakerDelProveedor.cs` — conteo de fallos consecutivos, apertura y media apertura con reloj inyectado
- [x] 3.3 `Infrastructure/ProveedorConBreaker.cs` — decorador que consulta y alimenta el breaker
- [x] 3.4 Contar solo fallos de transporte y timeout; nunca rechazos semánticos
- [x] 3.5 En prueba, dejar pasar **una sola** llamada concurrente
- [x] 3.6 `OpcionesAsistente` — umbral de fallos y espera antes de probar
- [x] 3.7 Orden de los decoradores en `ModuleExtensions`: techo → breaker → proveedor
- [x] 3.8 Test: el breaker abre al alcanzar el umbral
- [x] 3.9 Test: abierto, el proveedor no recibe nada
- [x] 3.10 Test: un rechazo semántico no lo abre
- [x] 3.11 Test: tras la espera pasa **una sola** llamada de prueba con varios turnos concurrentes
- [x] 3.12 Test: la prueba exitosa cierra y la fallida reabre
- [x] 3.13 Test: el orden de los decoradores — con el breaker abierto no se consume cupo del actor

## 4. Resolución degradada del turno (ARS-54, segunda mitad)

- [x] 4.1 `Application/DisponibilidadDelModelo.cs` — el veredicto que la capa consulta
- [x] 4.2 `CapaConversacional` lo resuelve una vez al entrar y lo consulta solo donde hace falta
- [x] 4.3 El reescritor se saltea sin modelo, y la pregunta va cruda al resto del pipeline
- [x] 4.4 El carril SQL no se invoca sin modelo: se resuelve degradado antes
- [x] 4.5 La cuota agotada entra por el mismo camino que el breaker abierto
- [x] 4.6 Test: con el breaker abierto, un saludo responde con cero llamadas
- [x] 4.7 Test: con el breaker abierto, una pregunta ambigua devuelve su menú
- [x] 4.8 Test: con el breaker abierto, la respuesta a una aclaración se reconoce
- [x] 4.9 Test: con el breaker abierto, una pregunta de datos resuelve degradado
- [x] 4.10 Test: sin cupo, un saludo sigue resolviendo con cero llamadas
- [x] 4.11 Verificar en rojo: tratar la falta de modelo como excepción rompe 4.6 y 4.7

## 5. Los dos registros (ARS-55)

- [x] 5.1 `database/asistente/002_asistente_registros.sql` — schema `asistente` y las dos tablas
- [x] 5.2 Registro operativo: actor, momento, carril, estado, llamadas, tokens, latencia, reintento, truncado
- [x] 5.3 Registro analítico: pregunta, categoría, estado y fecha **de tipo `date`**, sin hora ni actor
- [x] 5.4 Comentario en el `.sql` declarando que **no** se aplica `audit.attach`, con el motivo
- [x] 5.5 GRANT: la conexión dueña escribe; los roles de solo lectura no
- [x] 5.6 `Application/IRegistroDelTurno.cs` + `Infrastructure/RegistroDelTurno.cs` — escritura tolerante a fallos
- [x] 5.7 Enganchar la escritura al final de `CapaConversacional`, con la latencia medida punta a punta
- [x] 5.8 Declarar el schema `asistente` como **denegado** en el manifiesto de privilegios. El de sensibilidad no lo toca: clasifica columnas que el asistente puede leer, y éstas no lo son
- [x] 5.9 Test: el operativo no guarda el texto de la pregunta
- [x] 5.10 Test: el analítico no guarda actor
- [x] 5.11 Test: dos turnos del mismo día tienen la misma fecha analítica
- [x] 5.12 Test: no hay columna compartida que permita cruzarlos
- [x] 5.13 Test: un turno con filas sensibles no deja ningún valor en ningún registro
- [x] 5.14 Test: ninguna de las dos tablas tiene disparador de auditoría
- [x] 5.15 Test: escribir un turno no hace crecer `audit.change_log`
- [x] 5.16 Test: el rol de solo lectura no puede insertar
- [x] 5.17 Test: sin tablas, el turno responde igual y el fallo queda logueado

## 6. Purga y retención (ARS-56)

- [x] 6.1 `Infrastructure/PurgaDeRegistros.cs` — borrado por ventana, sobre las dos tablas
- [x] 6.2 Servicio hospedado que la corre periódicamente, apagable con el módulo
- [x] 6.3 `OpcionesAsistente` — ventana de retención (90 días) y período de la purga
- [x] 6.4 Test: lo más viejo que la ventana desaparece
- [x] 6.5 Test: lo de adentro de la ventana se conserva
- [x] 6.6 Test: dos corridas seguidas no fallan
- [x] 6.7 Verificar en rojo: purgar contra el reloj del sistema en vez del inyectado. **El primer intento no dio rojo**: el ancla del test estaba pegada a la fecha real. Se la corrió al futuro, y ahí sí discrimina

## 7. Documentación

- [x] 7.1 `docs/architecture/domains/asistente.md` — el presupuesto, el breaker y los dos registros
- [x] 7.2 `docs/architecture/data-model.md` — el schema `asistente` y sus dos tablas
- [x] 7.3 `backend/src/Modules.Asistente/README.md` — la configuración nueva y el orden de los decoradores
- [x] 7.4 `docs/quality/tech-debt.md` — la cuota en memoria se pierde en cada redespliegue

## 8. Guards de arquitectura que hubo que acotar

- [x] 8.1 `ArquitecturaAsistenteTests` — de «el módulo no escribe» a «el módulo no muta datos de otro schema»
- [x] 8.2 El detector extrae el objetivo de la sentencia y perdona **solo** `asistente.*`
- [x] 8.3 `DROP` y `ALTER` quedan prohibidos sin excepción, ni siquiera sobre lo propio
- [x] 8.4 `CadenaDuena` suma dos archivos a la lista blanca, con el motivo escrito por archivo
- [x] 8.5 Test nuevo: escribir el schema propio **no** es infracción y escribir uno ajeno **sí**
