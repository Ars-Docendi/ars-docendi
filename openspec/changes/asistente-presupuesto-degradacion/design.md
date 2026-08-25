# Diseño — Presupuesto, degradación y observabilidad

## Contexto

El pipeline del turno es hoy:

```
CapaConversacional
  ├─ enrutador social/meta       ← 0 tokens
  ├─ reconocedor de aclaración   ← 0 tokens
  ├─ detector de cambio de tema  ← 0 tokens
  ├─ reescritor                  ← 1 llamada, solo con historial
  ├─ detector de ambigüedad      ← 0 tokens (consulta a la base)
  └─ CarrilSql
       ├─ generación             ← 1 llamada (+1 de reintento)
       ├─ validador              ← 0 tokens
       ├─ ejecución              ← 0 tokens
       └─ redacción              ← 1 llamada
```

Cinco de los ocho pasos no necesitan proveedor. Ese es el hecho sobre el que se apoya toda esta épica.

Lo que ya existe: `ContadorDeLlamadasDelTurno` (techo global del turno) y `ProveedorConTechoDeLlamadas` (el decorador que lo cobra). Lo que falta: cuota entre turnos, cota de tiempo, breaker, y que la degradación no mate el turno entero.

## Decisiones

### D1 — La cuota se mide en llamadas al modelo y vive en la aplicación

Con una sola clave por ambiente, el proveedor factura al ambiente y no puede atribuir consumo por usuario. Si la cuota no vive acá, no existe.

Se mide en **llamadas al modelo**, no en requests HTTP, y la diferencia no es de matiz: un turno con reescritor cuesta tres llamadas y con reintento de transporte hasta cuatro requests por llamada. Contar requests HTTP del cliente subestimaría el consumo por un factor de tres; contar requests al proveedor lo sobreestimaría por el reintento, que ya tiene su propia cota.

El chequeo va **antes de todo el pipeline**. Superado el cupo, no se emite ninguna llamada — no una que falle, ninguna. El test lo verifica contando llamadas al proveedor, no leyendo el estado devuelto.

**El cargo se hace una vez por turno, no por llamada.** `ContadorDeLlamadasDelTurno` ya sabe exactamente cuántas llamadas costó el turno, y la capa conversacional es lo único que conoce al actor; cobrar en un `finally` al terminar usa las dos cosas que ya existen. Un decorador que cobrara por llamada necesitaría el actor, y el proveedor no lo recibe: habría que meterlo en un objeto de request mutable que las capas leen sin declararlo.

La consecuencia hay que decirla: un turno que arranca con una sola llamada de cupo puede excederlo por hasta tres antes de que el exceso se vea. El desbalance está acotado por el techo del turno y se aceptó a cambio de no tener estado implícito de request. El `finally` cubre el otro riesgo, que es más importante: un turno que se cae a la mitad paga igual lo que llegó a emitir, así que fallar no es una forma de consultar gratis.

### D2 — La cuota vive en memoria, y el costo de esa decisión se declara

Ventana deslizante en memoria, con la misma naturaleza que el almacén de hilos y la caché de idempotencia. Un redespliegue le devuelve el cupo a todo el mundo.

Se acepta, y el motivo es el modelo de amenaza: son ~30 usuarios institucionales autenticados, ninguno puede forzar un redespliegue, y el techo de gasto duro vive en la consola del proveedor por ambiente (RNF-12), no acá. La cuota de la aplicación es un mecanismo de **equidad entre usuarios**, no la última línea contra una factura.

Persistirla exigiría una tabla escrita en el camino caliente de cada llamada, con su propia retención de datos personales indirectos —quién consultó, cuándo y cuánto— sobre un sistema que decidió no persistir ni el hilo.

### D3 — El techo de tiempo es uno solo, punta a punta, y no la suma de los de cada etapa

Hoy hay `TimeoutDeSentenciaMs` y `TimeoutDeComandoSegundos` en la ejecución de SQL, y **nada** en las llamadas al modelo. Agregar un timeout por llamada no alcanza: cuatro llamadas de diez segundos son cuarenta segundos de espera, y cada una habría respetado su límite.

La cota es un único `CancellationTokenSource` con el presupuesto del turno, encadenado al token del request, creado al entrar a la capa conversacional y propagado hacia abajo. Las etapas siguen teniendo sus timeouts —el de sentencia libera el backend de la base, que el token no hace—, pero **ninguno de ellos es la cota del turno**.

Al vencer, el turno resuelve como servicio degradado y no como cancelación cruda: para quien preguntó, «no llegué a tiempo» es una respuesta.

La distinción entre «venció el presupuesto del turno» y «el usuario cerró la pestaña» se hace mirando el token del request, no el enlazado. Sin esa distinción, cada abandono del usuario se registraría como una degradación del servicio.

### D4 — El breaker es un decorador más, y el orden de los decoradores es parte del diseño

El proveedor se envuelve en este orden, de afuera hacia adentro:

```
ProveedorConTechoDeLlamadas   ← techo del turno (ya existía)
  └─ ProveedorConBreaker      ← estado del proveedor + timeout por llamada
       └─ proveedor real
```

De afuera hacia adentro es de más barato a más caro. El techo del turno es un contador en memoria y el breaker consulta un reloj: invertirlos haría que el breaker registrara intentos de llamadas que el techo iba a rechazar igual, y un turno desbocado terminaría abriendo el corte para todo el mundo.

La cuota no está en esta cadena, por D1: la cobra la capa. La propiedad que sí hay que verificar es que **con el breaker abierto no se consume cupo del actor**, y sale sola de que el veredicto se resuelve antes del pipeline: sin modelo no hay llamadas, y sin llamadas no hay nada que cobrar.

El timeout y el breaker viven en el mismo decorador porque el timeout es una de las dos formas de fallo que el breaker cuenta. Separarlos lo dejaría sin ver la mitad de los fallos que le importan.

El breaker cuenta fallos **de transporte y de timeout**, nunca rechazos semánticos: un modelo que devuelve una respuesta inútil está sano.

### D5 — La degradación se decide antes del pipeline y la capa la respeta paso a paso

El error a no cometer es tratar «no hay modelo» como una excepción que corta el turno. Si corta, el saludo deja de resolverse a cero tokens justo cuando más importa que resuelva, y la pregunta ambigua deja de devolver su menú aunque el menú salga de una consulta a la base.

La capa conversacional recibe el veredicto —`HayModelo` sí o no— **antes** de empezar, y lo consulta en el único paso que lo necesita (el reescritor) y al delegar en el carril SQL. Todo lo demás corre igual.

Consecuencia concreta y verificable: con el breaker abierto, «hola» responde, «¿cuál Análisis Matemático?» devuelve sus tres opciones, y solo una pregunta que exige generar SQL termina en servicio degradado.

### D6 — Los dos registros no se cruzan, y quitar la hora es lo que los desvincula

El registro analítico guarda el texto de la pregunta con la fecha **redondeada al día** y sin actor. El operativo guarda el actor con timestamp preciso y sin texto.

Redondear la fecha no es prolijidad: con ~30 usuarios, un timestamp preciso en ambos permitiría reidentificar al autor de cada pregunta con un `join` por tiempo. Desvincular sin quitar la hora no desvincula nada.

**Ninguno guarda las filas devueltas.** Ni por defecto ni detrás de un flag: son exactamente los datos que el enmascaramiento acaba de proteger. La SQL generada tampoco — un `WHERE` puede llevar un documento.

### D7 — Sin `audit.attach`, declarado en la migración

Todas las tablas del repositorio llaman a `audit.attach()` al final de su archivo. Es la convención, y acá hay que romperla: `audit.change_log` guarda la fila entera en JSON y **no tiene política de retención**, así que enganchar el registro analítico haría que el texto de cada pregunta sobreviviera a la purga en otra tabla.

Se declara explícito, con el motivo escrito en el `.sql`, porque una ausencia silenciosa la completa el próximo que agregue una tabla por consistencia.

### D8 — El escritor de registros nunca hace fallar un turno

Un registro que rompe el turno que estaba registrando convierte la observabilidad en una fuente de indisponibilidad. Los dos escriben en modo tolerante: el fallo se loguea y el turno sigue.

Es la decisión inversa a la del enmascarador, y a propósito: ahí un fallo silencioso filtra datos, acá un fallo ruidoso niega un servicio que funciona.

### D9 — La purga corre en el proceso, no en la base

Un `pg_cron` exigiría una extensión más en la provisión de cada ambiente. Un servicio hospedado del propio Host corre donde ya corre todo, se testea con el resto y se apaga con el módulo (RNF-20).

Es idempotente y no falla si no hay nada que borrar: la ventana se compara contra el reloj inyectado, así que el test adelanta el tiempo en vez de esperar 90 días.

## Alternativas descartadas

| Alternativa | Por qué no |
| --- | --- |
| Cuota derivada del registro operativo | Acopla el camino caliente a una tabla con retención propia: al purgar a los 90 días, el cupo se recalcularía sobre datos incompletos. Y agrega una lectura a la base antes de cada turno. |
| Timeout por etapa, sin cota global | Es exactamente el modo de falla que RNF-09 nombra: cada etapa respeta su límite y el total se suma. |
| Breaker por proceso compartido entre ambientes | El proveedor puede estar caído para una clave y sano para otra; y el módulo se despliega por ambiente. |
| Modo degradado como excepción que corta el turno | Apagaría los cinco pasos que funcionan sin proveedor, justo cuando son lo único que queda. |
| Registro único con un flag de anonimato | Un flag no desvincula: las dos filas siguen en la misma tabla con la misma clave y el mismo tiempo. |
