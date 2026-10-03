## 1. Reparto de proyectos (ARS-63)

- [x] 1.1 `backend/src/ArsDocendi.Evaluacion.Nucleo/` — biblioteca **en** la solución, con todo lo que no cuesta dinero
- [x] 1.2 `backend/eval/ArsDocendi.Evaluacion/` — consola **fuera** de la solución, único lugar que instancia un proveedor real
- [x] 1.3 Agregar el núcleo a `backend/ArsDocendi.slnx`; **no** agregar el runner
- [x] 1.4 Test guard: el archivo de solución no nombra el proyecto del runner
- [x] 1.5 Test anti-vacuidad: el guard leyó el archivo y encontró proyectos
- [x] 1.6 Test: el núcleo sí está en la solución
- [x] 1.7 Test: ningún proyecto de la solución referencia al runner
- [x] 1.8 Test: el proveedor por omisión de la configuración del módulo es el simulado
- [x] 1.9 Verificar el guard en rojo: agregar el runner al archivo de solución y comprobar que falla nombrando el proyecto
- [x] 1.10 `backend/eval/README.md` — cómo correr el eval a mano y qué hace falta

## 2. Fixture determinista (ARS-57)

- [x] 2.1 Generador con fecha ancla fija, identificadores derivados del índice y ninguna función de reloj
- [x] 2.2 Una fuente de aleatoriedad **por sección**, sembrada con un valor derivado del nombre de la sección
- [x] 2.3 Colisiones garantizadas: nombres de materia repetidos entre carreras y apellidos compartidos, con cardinalidades declaradas
- [x] 2.4 Un solo período activo y designaciones con vigencia abierta, para que «lo actual» no dependa del calendario
- [x] 2.5 Exponer la huella del fixture
- [x] 2.6 Test: dos ejecuciones producen el mismo contenido byte a byte
- [x] 2.7 Test: el contenido no usa funciones de reloj
- [x] 2.8 Test: las cardinalidades de colisión son las declaradas
- [x] 2.9 Test: agregar un elemento a una sección no corre los valores de las siguientes
- [x] 2.10 Test: hay exactamente un período activo y hay designaciones con vigencia abierta
- [x] 2.11 Test: la huella es estable y cambia cuando cambia el fixture
- [x] 2.12 Test de integración: el fixture aplica sin error sobre una base migrada

## 3. Dataset de capacidad (ARS-58, primera mitad)

- [x] 3.1 `backend/eval/datasets/capacidad.json` — dataset versionado, estratificado por las seis categorías
- [x] 3.2 Cada ítem declara categoría, actor y —si es factible— consulta de referencia
- [x] 3.3 Los ítems que declaran el orden como parte de la pregunta lo marcan explícitamente
- [x] 3.4 Test: cada ítem tiene una categoría de la lista cerrada y un actor
- [x] 3.5 Test: las seis categorías están representadas
- [x] 3.6 Test: un ítem no contestable o ambiguo no trae consulta de referencia
- [x] 3.7 Test: toda consulta de referencia pasa el validador
- [x] 3.8 Test de integración: toda consulta de referencia ejecuta sin error contra el fixture
- [x] 3.9 Test: el dataset es **disjunto** del catálogo de ejemplos — cierra la tarea 3.15 del cambio del carril

## 4. Puntuación (ARS-58, segunda mitad)

- [x] 4.1 Comparación por conjunto de filas, ignorando nombres de columna
- [x] 4.2 El orden se ignora salvo que el ítem lo declare
- [x] 4.3 Puntuación con penalización: acierto factible y abstención infactible suman; abstención factible no suma; error factible e intento infactible restan
- [x] 4.4 La abstención solo acredita **sin error**
- [x] 4.5 Reportar con tres valores de penalización
- [x] 4.6 Test: cada uno de los cinco desenlaces puntúa como corresponde
- [x] 4.7 Test: una abstención con error no acredita
- [x] 4.8 Test: con todos los turnos fallados, el eje de abstención no muestra aciertos
- [x] 4.9 Test: dos consultas distintas con el mismo resultado aciertan
- [x] 4.10 Test: un alias distinto no cuenta como error
- [x] 4.11 Test: el orden importa solo cuando el ítem lo declara
- [x] 4.12 Test: las tres penalizaciones cambian el puntaje y no los conteos
- [x] 4.13 Test: la puntuación no consume ninguna llamada al modelo

## 5. Runner y sellado (ARS-61)

- [x] 5.1 Preflight que verifica ausencia de excepción, respuesta no simulada y tokens mayores que cero
- [x] 5.2 Abortar con código distinto de cero cuando el preflight falla
- [x] 5.3 **No escribir ningún reporte** cuando el preflight falla
- [x] 5.4 Sellar cada reporte con el hash del prefijo, el del dataset y el del fixture
- [x] 5.5 Derivar los conteos del reporte del dataset efectivamente cargado
- [x] 5.6 Formato de reporte generado, con conteos por categoría y los tres puntajes
- [x] 5.7 Test: un proveedor que falla aborta con código distinto de cero
- [x] 5.8 Test: el proveedor simulado se rechaza
- [x] 5.9 Test: una respuesta con tokens en cero se rechaza
- [x] 5.10 Test: con el preflight fallido el directorio de reportes queda vacío
- [x] 5.11 Test: con el preflight fallido un reporte anterior queda intacto
- [x] 5.12 Test: el encabezado trae los tres hashes
- [x] 5.13 Test: cambiar el dataset cambia el hash del dataset del reporte
- [x] 5.14 Test: el total del reporte coincide con el número de ítems del archivo
- [x] 5.15 Test: los conteos por categoría suman el total
- [x] 5.16 Test: dos corridas con proveedor determinista producen reportes idénticos
- [x] 5.17 Test: la llamada del preflight no se cuenta como ítem

## 6. Documentación en el mismo cambio

- [x] 6.1 `backend/eval/README.md` — la métrica, los ejes, cómo se lee un reporte, cómo se corre
- [x] 6.2 `docs/quality/` — dónde vive la evaluación y por qué está fuera del CI
- [x] 6.3 `backend/src/Modules.Asistente/README.md` — enlazar la evaluación
- [x] 6.4 `docs/architecture/domains/asistente.md` — sumar la evaluación a las specs activas
- [x] 6.5 Anotar explícitamente qué falta para poder correrlo: una implementación de proveedor real y una clave
