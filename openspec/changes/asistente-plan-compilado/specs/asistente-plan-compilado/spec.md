## Purpose

Permite responder preguntas sobre docentes con designación vigente —simples o compuestas— sin que el modelo escriba SQL: el modelo traduce la pregunta a un plan tipado sobre un catálogo cerrado, y el sistema lo valida, lo compila a SQL certificado, lo ejecuta bajo el alcance del actor y redacta la respuesta por plantilla. Ante cualquier duda, el sistema aclara o se abstiene en lugar de responder.

## ADDED Requirements

### Requirement: El plan compilado está apagado por omisión

Con `PlanCompilado` ausente o en `false`, el sistema SHALL resolver cada turno exactamente como antes de que existiera esta capacidad: ninguna solicitud al proveedor ni ninguna respuesta MUST cambiar.

#### Scenario: Opción apagada

- **GIVEN** las opciones con sus valores por omisión
- **WHEN** un actor hace una pregunta sobre docentes
- **THEN** el turno se resuelve por el carril SQL sin generar ningún plan

### Requirement: Solo las preguntas dentro del catálogo intentan un plan

Con la opción encendida, el sistema SHALL intentar un plan únicamente si la pregunta menciona a la población de docentes, lo que pide son personas y no contiene vocabulario de dominio fuera del catálogo. Una pregunta que no cumple MUST seguir por el carril SQL sin gastar ninguna llamada al modelo en el plan.

#### Scenario: Pregunta sobre pedidos

- **GIVEN** la opción encendida
- **WHEN** el actor pregunta «¿cuántos pedidos rechazados hay?»
- **THEN** el turno sigue por el carril SQL y no se pide ningún plan

#### Scenario: Pregunta que pide materias y nombra a la población

- **GIVEN** la opción encendida
- **WHEN** el actor pregunta «¿qué materias de Ingeniería Electrónica tienen algún profesor titular designado?»
- **THEN** el turno sigue por el carril SQL y no se pide ningún plan

#### Scenario: Pregunta compuesta sobre docentes

- **GIVEN** la opción encendida
- **WHEN** el actor pregunta «¿cuántos titulares dictan en al menos dos carreras?»
- **THEN** el sistema pide un plan al modelo

### Requirement: Un concepto con dos definiciones se aclara sin llamar al modelo

El sistema SHALL pedir aclaración, sin llamar al modelo, cuando la pregunta menciona «antigüedad» sin indicar si se cuenta desde la primera designación o desde la experiencia declarada en el portal. La aclaración MUST ofrecer una opción por definición.

#### Scenario: Antigüedad sin calificar

- **GIVEN** la opción encendida
- **WHEN** el actor pregunta «¿cuántos titulares tienen más de 20 años de antigüedad?»
- **THEN** el turno termina en `NecesitaAclaracion` con dos opciones y cero llamadas al modelo

#### Scenario: Antigüedad calificada

- **GIVEN** la opción encendida
- **WHEN** el actor pregunta «¿cuántos titulares tienen más de 20 años de antigüedad desde su primera designación?»
- **THEN** el sistema pide un plan al modelo

### Requirement: El plan se genera con salida estructurada y se acepta solo por acuerdo

El sistema SHALL pedir la primera muestra del plan a temperatura 0 y con el esquema JSON del catálogo como salida estructurada. Si la primera muestra es válida, SHALL pedir `MuestrasDelPlan − 1` muestras más a `TemperaturaDeMuestrasDelPlan`. El sistema MUST ejecutar el plan solo si todas las muestras son válidas y su forma canónica es idéntica.

#### Scenario: Todas las muestras coinciden

- **GIVEN** `MuestrasDelPlan = 3` y tres muestras válidas con la misma forma canónica
- **WHEN** termina el muestreo
- **THEN** el plan se compila y se ejecuta

#### Scenario: Las muestras no coinciden

- **GIVEN** `MuestrasDelPlan = 3` y dos interpretaciones válidas distintas entre las muestras
- **WHEN** termina el muestreo
- **THEN** el turno termina en `NecesitaAclaracion` con una opción por interpretación y no se ejecuta ninguna consulta

#### Scenario: La pregunta no es expresable

- **GIVEN** una primera muestra con `expresable: false`
- **WHEN** termina la primera llamada
- **THEN** no se piden más muestras y el turno sigue por el carril SQL

#### Scenario: La primera muestra es inválida

- **GIVEN** una primera muestra con un campo fuera del catálogo o una condición sin ancla en la pregunta
- **WHEN** termina la primera llamada
- **THEN** el turno se abstiene sin ejecutar ninguna consulta y sin seguir por el carril SQL

### Requirement: Cada condición del plan está anclada en la pregunta

El sistema MUST rechazar una muestra si alguna de sus condiciones no tiene en el texto de la pregunta su término, su valor y, para los operadores de comparación, una señal compatible; o si algún término del catálogo presente en la pregunta no aparece en el plan. Una condición de categoría, de cantidad de carreras o de cantidad de materias cuyo número no tiene señal de comparación MUST leerse como igualdad.

#### Scenario: Condición inventada

- **GIVEN** la pregunta «¿cuántos titulares hay?»
- **WHEN** la muestra agrega una condición de cantidad de carreras
- **THEN** la muestra es inválida

#### Scenario: Término sin consumir

- **GIVEN** la pregunta «¿cuántos titulares de Ingeniería Industrial hay?»
- **WHEN** la muestra filtra solo por cargo titular
- **THEN** la muestra es inválida

#### Scenario: Categoría sin señal de comparación

- **GIVEN** la pregunta «¿cuántos docentes tienen alguna designación de categoría 5?»
- **WHEN** la muestra compara la categoría con «mayor o igual a 5»
- **THEN** la condición se lee como «igual a 5» y la muestra coincide con la que escribió la igualdad

#### Scenario: Cantidad sin señal de comparación

- **GIVEN** la pregunta «¿cuántos titulares dictan en dos carreras?»
- **WHEN** la muestra compara la cantidad de carreras con «mayor o igual a 2»
- **THEN** la condición se lee como «igual a 2» y el turno responde por exactamente dos carreras

### Requirement: Las entidades se resuelven en el servidor dentro del alcance del actor

El sistema SHALL resolver cada materia y carrera del plan contra las entidades que el actor puede ver, por coincidencia exacta del nombre normalizado, y MUST ligarlas a la consulta como parámetros, nunca como texto. Una entidad que no resuelve MUST producir una abstención que la nombre; una entidad con más de una coincidencia distinta MUST producir una aclaración.

#### Scenario: Carrera inexistente

- **GIVEN** un plan que filtra por la carrera «Ingeniería Química»
- **WHEN** se resuelven las entidades
- **THEN** el turno se abstiene indicando que no encontró esa carrera

#### Scenario: Materia compartida por varias carreras

- **GIVEN** un plan que filtra por «Análisis Matemático», que existe en tres carreras, sin una carrera en la misma lista
- **WHEN** se resuelven las entidades
- **THEN** el turno termina en `NecesitaAclaracion` con una opción por carrera, y cada opción nombra su carrera

### Requirement: La consulta compilada respeta las barreras del carril SQL

El sistema SHALL compilar el plan a una única consulta de lectura que pase `ValidadorDeSql`, que no use el reloj del servidor, que se ejecute con el rol básico del asistente y que quede acotada por RLS al ámbito del actor.

#### Scenario: Actor acotado a una carrera

- **GIVEN** un coordinador de una carrera
- **WHEN** pregunta cuántos titulares hay
- **THEN** el conteo incluye solo a quienes tienen designaciones vigentes en materias que puede ver, y la respuesta lo aclara

### Requirement: La respuesta se arma por plantilla con la interpretación a la vista

El sistema SHALL redactar la respuesta sin llamar al modelo, a partir del resultado y del plan, y MUST incluir la interpretación del plan. Un porcentaje MUST mostrar su numerador y su denominador. Un denominador cero MUST responderse como tal, sin calcular un porcentaje.

#### Scenario: Porcentaje

- **GIVEN** un plan de porcentaje con 1 docente que cumple sobre 4
- **WHEN** se redacta la respuesta
- **THEN** la respuesta dice 25 % y «1 de 4», junto con la interpretación
