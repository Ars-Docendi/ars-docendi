## 1. Eje de robustez de fraseo (ARS-59, primera mitad)

- [x] 1.1 `Dataset/DatasetDeRobustez.cs` — el ítem hereda consulta, categoría, actor y orden del origen
- [x] 1.2 Clases de perturbación cerradas: sin tildes, tipeo, sinónimo, parcial, coloquial
- [x] 1.3 Rechazar al cargar: origen inexistente, consulta propia declarada, clase desconocida
- [x] 1.4 `Runner/RunnerDeRobustez.cs`, reusando la evaluación del eje de capacidad
- [x] 1.5 `backend/eval/datasets/robustez.json` — al menos dos perturbaciones por clase
- [x] 1.6 Test: la consulta es byte-idéntica a la del origen
- [x] 1.7 Test: un origen inexistente hace fallar la carga
- [x] 1.8 Test: un ítem con consulta propia hace fallar la carga
- [x] 1.9 Test: la pregunta difiere de la del origen y el resto no
- [x] 1.10 Test: el reporte desagrega por clase de perturbación

## 2. Eje de diálogo (ARS-59, segunda mitad)

- [x] 2.1 `Dataset/DatasetDeDialogo.cs` — conversaciones, turnos y términos prohibidos
- [x] 2.2 `Runner/RunnerDeDialogo.cs` — corre los turnos sobre el mismo hilo
- [x] 2.3 El chequeo negativo mira la **pregunta interpretada**, no la respuesta
- [x] 2.4 `backend/eval/datasets/dialogo.json` — seguimiento, aclaración y **pivote duro**
- [x] 2.5 Test: los turnos comparten hilo
- [x] 2.6 Test: un término arrastrado hace fallar el turno
- [x] 2.7 Test: el chequeo es sensible — una interpretada construida para arrastrar falla
- [x] 2.8 Test: el dataset tiene un pivote duro, sin anáfora y con términos prohibidos
- [x] 2.9 Test: un turno caído no invalida los anteriores

## 3. Eje social y meta (ARS-60)

- [x] 3.1 `Runner/MedidorDeConsumo.cs` — decorador del proveedor que además es el instrumento
- [x] 3.2 `Dataset/DatasetSocial.cs` — clases social, no contestable y negativo
- [x] 3.3 `Runner/RunnerSocial.cs` — assert de cero tokens, sugerencias exigidas y negativos
- [x] 3.4 Abortar sin reporte si **todos** los turnos consumieron cero tokens
- [x] 3.5 `backend/eval/datasets/social.json`, con negativos tomados del eje de capacidad
- [x] 3.6 Test: un social que costó tokens falla
- [x] 3.7 Test: un no contestable sin sugerencias falla
- [x] 3.8 Test: un negativo capturado por el enrutador falla
- [x] 3.9 Test: con todos los turnos a cero, el runner aborta y no deja reporte
- [x] 3.10 Test: una corrida sana no aborta
- [x] 3.11 Verificar en rojo: medir llamadas en vez de tokens. **No dio rojo**, y hay que decirlo: el proveedor guionado siempre devuelve tokens cuando se lo llama, así que con él `llamadas == 0` y `tokens == 0` coinciden. La distinción solo es observable contra un proveedor real que pueda devolver una respuesta de cero tokens. El código mide tokens igual, porque es lo que el requisito dice

## 4. Gate de regresión (ARS-62)

- [x] 4.1 `Runner/LineaDeBase.cs` — veredicto por identificador más los tres hashes
- [x] 4.2 `Runner/GateDeRegresion.cs` — comparación por ítem
- [x] 4.3 Con cualquier hash distinto, exigir regenerar en vez de comparar
- [x] 4.4 La regeneración es explícita y nunca efecto de una corrida
- [x] 4.5 `backend/eval/lineas-de-base/README.md` — qué es y cuándo se regenera
- [x] 4.6 Test: un ítem roto hace fallar el gate aunque el promedio suba
- [x] 4.7 Test: una corrida idéntica pasa
- [x] 4.8 Test: un ítem arreglado se informa como mejora y no falla
- [x] 4.9 Test: un ítem nuevo se informa como nuevo y no falla
- [x] 4.10 Test: un hash distinto detiene la comparación
- [x] 4.11 Test: correr el eje no reescribe la línea de base

## 5. Composición y documentación

- [x] 5.1 `backend/eval/ArsDocendi.Evaluacion/Program.cs` — los cuatro ejes y el gate
- [x] 5.2 Cuatro reportes en archivos separados, y dicho en la salida que no se promedian
- [x] 5.3 `backend/eval/README.md` — los cuatro ejes, el gate y por qué no se promedian
- [x] 5.4 `docs/architecture/domains/asistente.md` — la evaluación completa

## 6. Un defecto encontrado al escribir los tests

- [x] 6.1 Los tres runners sostenían UNA instancia del pipeline para todo el dataset, así que el techo de llamadas **por turno** funcionaba como techo de la **corrida**
- [x] 6.2 El tercer ítem ya lo agotaba: resolvía degradado y el eje reportaba fallo casi total, sin error — solo un número
- [x] 6.3 Los tres pasan a recibir una fábrica y arman el pipeline por ítem
- [x] 6.4 Test que lo fija: tres ítems con techo de dos llamadas resuelven los tres
- [x] 6.5 Verificado en rojo: con una instancia compartida, ese test cae
