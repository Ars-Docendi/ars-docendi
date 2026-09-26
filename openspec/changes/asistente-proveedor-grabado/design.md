## Context

El módulo ya tiene todas las piezas que este cambio necesita, y ninguna hay que inventarla:

- **`ModuleExtensions` arma un cliente HTTP con nombre**, `ClienteDelProveedor`, con `ReintentoDeTransporte` como único `DelegatingHandler`. `ProveedorAnthropic` lo recibe de afuera y se lo pasa al SDK. Ese «de afuera» es literalmente lo que hoy permite probar el adaptador sin clave y sin red, y es el mismo agujero por el que entra la grabación.
- **`TransporteFalso`** ya intercepta ese cable en los tests: guarda los cuerpos que salen y devuelve respuestas con la forma exacta de la API de mensajes. Su propio comentario explica por qué se prueba contra el cable y no contra un doble del SDK.
- **`ProveedorSimulado.Huella`** ya calcula una huella determinista con SHA-256 y ya tiene escrito por qué no usa `string.GetHashCode()`: en .NET el hash de string está aleatorizado por proceso.
- **`SelloDeIdentidad`** del evaluador ya sella los reportes con tres huellas (prefijo, dataset, fixture), que es lo que RNF-03 exige.
- **`ArquitecturaAsistenteTests.El_SDK_del_proveedor_se_nombra_en_un_solo_archivo`** fija que nada fuera de `ProveedorAnthropic.cs` nombre el SDK. La excepción es una y no se amplía.

Lo que falta es que algo escriba a disco lo que pasa por ese cable, y lo vuelva a leer.

## Goals / Non-Goals

**Goals:**

- Que el parseo del adaptador y todo lo que viene después —`GeneradorDeSql.Interpretar`, el redactor, el reescritor— corra contra cuerpos de respuesta que un modelo produjo de verdad.
- Que la primera corrida financiada del evaluador deje, además del reporte, un activo permanente que sobreviva al presupuesto que lo produjo.
- Que el camino de grabar y reproducir esté probado punta a punta **sin clave y sin red**, para que el día de la corrida no se descubra que el mecanismo no andaba.
- Que sea imposible que el CI haga una llamada de red por este mecanismo.

**Non-Goals:**

- Reemplazar a `ProveedorGuionado`. Sigue siendo lo correcto para afirmar sobre lo que se le **manda** al modelo; los cassettes cubren la mitad de vuelta.
- Grabar la corrida financiada. La bloquea ARS-67; este cambio deja el mecanismo listo.
- Grabar los fallos de transporte (ver D2).
- Volver reproducible la _calidad_ de la traducción. Un cassette congela una respuesta, no la competencia del modelo: eso lo mide el evaluador.

## Decisions

### D1 — Un `DelegatingHandler` en el pipeline, no un decorador del puerto

La grabación va como handler del cliente HTTP con nombre, al lado de `ReintentoDeTransporte`.

**La alternativa era un decorador de `IProveedorDeModelo`**, que es más fácil de escribir: se serializa `SolicitudAlModelo` y `RespuestaDelModelo` y listo. Se descarta porque graba **la respuesta ya procesada**, y entonces el parseo del adaptador —que es la mitad no cubierta y el motivo del cambio— queda del lado de afuera del cassette. Un cassette de `RespuestaDelModelo` prueba el pipeline con nuestro propio parseo ya aplicado: exactamente lo que hoy hace `ProveedorGuionado`, con más maquinaria.

**Y hay un segundo motivo, de arquitectura.** El handler ve cuerpos HTTP, no tipos del SDK. Los nombres que lee —`system`, `messages`, `model`, `output_config`— son campos del cable, no símbolos de la librería, así que el guard que fija el SDK en un solo archivo sigue en pie **sin excepción nueva**. Un decorador del puerto tampoco lo rompería, pero tampoco cubriría lo que hay que cubrir.

**Costo aceptado**: el handler tiene que entender la forma del cuerpo lo suficiente como para sacar cuatro campos. Es acoplamiento al formato del cable, y está asumido: es el mismo formato del que depende el cassette entero.

### D2 — El grabador va por fuera del reintento

En `AddHttpMessageHandler` el orden de registración es de afuera hacia adentro: el primero que se agrega es el que está más cerca de quien llama. El grabador se registra **antes** que `ReintentoDeTransporte`, así que el pipeline queda:

```
adaptador → grabador → reintento → transporte
```

Con ese orden, el grabador ve **una solicitud por llamada lógica** y la respuesta que el reintento resolvió: la que el pipeline efectivamente le devolvió al adaptador.

**La alternativa era ponerlo del lado de adentro**, viendo cada intento. Tiene un atractivo real —grabaría también los fallos, y con ellos el 429 y el 503 verdaderos del proveedor— y se descarta por tres cosas concretas:

1. **Rompe la identidad del cassette.** Los cuatro campos de la clave son de la solicitud, y la solicitud es la misma en los tres intentos. Distinguirlos exigiría meter el número de intento en la clave, que es estado del transporte y no de la pregunta.
2. **Reproducir un fallo reproduce la espera.** El reintento haría su backoff de verdad al replay, y una suite que duerme por un cassette es una suite que alguien va a apagar.
3. **No cubre nada que no esté cubierto.** `ReintentoYTechoTests` ya ejercita el reintento contra el cable con `TransporteFalso`, incluido el `retry-after`. Lo que no está cubierto es el parseo de una respuesta **exitosa** real, y esa es la que este orden graba.

**Costo aceptado**: nunca vamos a tener un cassette de un 429 real. Si algún día hace falta, se graba a mano con un cassette sintético, que es lo que los tests del reintento ya hacen.

### D3 — La clave sale de cuatro campos del cuerpo, no del cuerpo entero

Huella SHA-256 de (prefijo estable, mensaje, esfuerzo, modelo), unidos con separador, con el mismo criterio que `ProveedorSimulado.Huella` y por la razón que ya está escrita ahí.

**La alternativa era hashear el cuerpo completo**, que es más simple y más honesta con «esta clave identifica este request». Se descarta por el modo de fallar: subir `MaximoDeTokensDeGeneracion` —que es una perilla que la propia documentación de `OpcionesAsistente` invita a mover cuando aparece el aviso de corte— invalidaría **todos** los cassettes de golpe, y recuperarlos costaría otra corrida financiada. Los cuatro campos son los que determinan qué contesta el modelo; el techo de tokens determina cuánto, y eso ya viaja adentro de la respuesta grabada.

**Costo aceptado**: dos solicitudes que difieren solo en un campo fuera de los cuatro comparten cassette. Es deliberado y es el punto.

### D4 — El cuerpo se guarda tal cual llegó

El cassette es un sobre JSON con el sello arriba y el cuerpo de la respuesta como cadena **verbatim**, sin reindentar ni reordenar.

**La alternativa era embeberlo como JSON parseado**, que se lee muchísimo mejor en un diff. Se descarta porque un cuerpo reserializado es un registro de **nuestro** serializador, no del suyo: el día que el proveedor cambie el orden de las claves, agregue un campo o mande un escape distinto, el cassette lo taparía. Eso es precisamente lo que la fixture existe para no tapar.

**Costo aceptado**: el archivo se revisa peor. Se acota poniendo el sello arriba y el cuerpo abajo, así lo que un revisor necesita mirar está antes del muro de texto.

### D5 — El sello lleva cuatro campos y no tres

Modelo, fecha, hash del prefijo —los tres que pide el ticket, y los mismos que RNF-03 le exige a los reportes— **más el hash del fixture** contra el que se grabó.

El cuarto no es decoración: es lo que hace mecánica la garantía de «ningún cassette tiene filas reales». Sin él, «se graba contra el fixture sintético» es una convención que se cumple hasta que alguien grabe contra su base de desarrollo con datos importados. Con él, un cassette que no declare el hash del fixture vigente **no se sirve**, y el guard que barre el repositorio tiene contra qué comparar.

Importa porque los cassettes de la llamada de redacción llevan filas adentro: la redacción recibe el resultado ya enmascarado, pero enmascarado no es sintético.

**Costo aceptado**: cambiar el fixture invalida los cassettes. Es correcto —una respuesta grabada sobre otros datos describe otro sistema— y es visible: el evaluador ya recalcula ese hash en cada corrida.

### D6 — Falla cerrado quiere decir que el handler no llama hacia adentro

Sin cassette y sin la variable de re-grabación, el handler lanza **sin invocar `base.SendAsync`**. No es un detalle de estilo: es la única forma de que «nunca una llamada de red en CI» sea una propiedad del código y no una promesa. El test que lo fija cuenta las llamadas al transporte de adentro y exige cero.

El error nombra la clave que faltó y el directorio donde se la buscó, porque el modo de fallar esperado es «alguien cambió una pregunta del dataset y todavía no la grabó», y ese diagnóstico se resuelve leyendo el mensaje.

**Con el cassette presente, la re-grabación no lo pisa.** Re-grabar es una operación deliberada sobre un directorio vacío o sobre las claves que faltan, no un modo en el que cada corrida gasta plata en respuestas que ya están.

### D7 — El camino punta a punta se prueba con un transporte que impersona la API, no con un servidor local

El test de grabación y reproducción arma **el mismo pipeline** —grabador más reintento— con un handler terminal que responde con la forma exacta de la API de mensajes, graba a un directorio temporal, y después arma un segundo pipeline en modo reproducción cuyo handler terminal **falla si alguien lo llama**. Si el cassette se lee, la respuesta traducida sale igual; si no se lee, el test se entera porque el terminal explota.

**La alternativa era levantar un servidor local que impersone la API**, apuntando el SDK a `localhost`. El SDK lo soporta —expone una URL base— y sería más fiel. Se descarta por lo que arrastra: una opción de configuración nueva cuyo único consumidor son los tests, un puerto que ligar en CI, y una perilla que redirige a dónde viaja la credencial. Esa última no es hipotética: el módulo ya tiene un guard dedicado a que la clave no se filtre, y agregar «y además el destino es configurable» amplía esa superficie a cambio de fidelidad que el handler terminal ya da.

`TransporteFalso` ya produce cuerpos con la forma de la API y ya se usa para probar el adaptador real sin clave. Es la pieza que corresponde.

### D8 — Los cassettes se versionan en el repositorio

Viven bajo el proyecto de tests, en un directorio propio, y entran al repositorio como cualquier otro fixture. Son el activo que este cambio existe para producir: un cassette que no se commitea es una corrida financiada tirada.

El directorio y la variable de re-grabación son **dos opciones nuevas por ambiente**, las dos vacías por default. Con el directorio vacío el handler ni siquiera se registra, así que producción no paga nada y no hay nada que se pueda misconfigurar: la única forma de encender el mecanismo es escribir una ruta.

### D9 — Los tests de parseo iteran el directorio

Un caso por cassette encontrado, no un test por cassette escrito a mano. El día que la corrida deje cuarenta, son cuarenta casos sin tocar un archivo de test.

El complemento obligatorio es que **un directorio vacío falle**: una suite que itera una lista vacía pasa en verde con cero cobertura, que es el peor resultado posible para un mecanismo cuyo propósito entero es cubrir algo.

## Risks / Trade-offs

- **Fixture congelada sin recaptura programada** → No detecta un cambio de formato de cable del proveedor: los cassettes siguen pasando en verde mientras la API real devuelve otra cosa. Es el mismo hueco que tiene la suite VCR de Metabase y no tiene solución barata. Se registra como **TD-017**, con la mitigación posible anotada: la corrida de evaluación siguiente vuelve a grabar, y esa sí habla con la API real.
- **Un cassette es una respuesta, no una garantía de calidad** → Congelar la salida de un modelo hace que los tests midan el parseo, no la traducción. Está dicho en los Non-Goals y es responsabilidad del evaluador; el riesgo real es que alguien lea «tests contra el modelo real» y crea que la calidad quedó cubierta. Se mitiga escribiéndolo donde se lee: en el README del módulo y en el del evaluador.
- **El acoplamiento al formato del cuerpo vive en dos lugares** → El adaptador lo conoce por el SDK y el grabador por los cuatro campos que extrae. Un cambio de formato rompe los dos, pero el grabador rompe en silencio: dejaría de encontrar el campo y produciría claves distintas. Se mitiga fallando ruidoso cuando un campo esperado no está, en vez de hashear una cadena vacía.
- **Los cassettes de redacción llevan filas** → Enmascaradas, pero filas al fin. La mitigación es el hash del fixture en el sello (D5) más el guard que barre el repositorio; sin eso, la disciplina se apoya en que quien grabe se acuerde.
- **Re-grabar cuesta plata** → La variable de re-grabación es la única perilla que puede gastar, y está apagada por default y ausente del CI por construcción (el ejecutable del evaluador está fuera de la solución y `ExclusionDelEvaluadorTests` lo vigila). Se mitiga además con D6: con el cassette presente no se re-graba aunque la variable esté puesta.

## Migration Plan

No hay migración de datos ni de schema. El despliegue es la registración condicional del handler, que con la configuración por default no altera el pipeline.

**Rollback**: dejar el directorio de cassettes sin configurar apaga el mecanismo entero sin tocar código. Revertir el commit tampoco deja estado que limpiar: lo único que este cambio escribe fuera del código son archivos dentro del repositorio.

## Open Questions

- **Cuántos cassettes deja la corrida financiada.** Depende del recorte del dataset con el que se corra, y se decide con ARS-67 en la mano. No bloquea nada acá: el mecanismo no tiene número adentro.
- **Si conviene grabar también las llamadas del preflight.** Son triviales y no ejercitan ningún parseo interesante; queda para verlo con los cassettes reales delante.
