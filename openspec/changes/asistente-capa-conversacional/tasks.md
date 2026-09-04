## 1. Hilo conversacional (ARS-38)

- [x] 1.1 `Application/HiloConversacional.cs` — el hilo: actor dueño, turnos, inicio de segmento y aclaración pendiente
- [x] 1.2 Guardar por turno la pregunta interpretada y su marca de tiempo, y **nada de las filas**
- [x] 1.3 `HistorialVigente()` — recorta desde el inicio del segmento, con tope de turnos
- [x] 1.4 `Application/IAlmacenDeHilos.cs` + `Infrastructure/AlmacenDeHilosEnMemoria.cs` — resolución por identificador, atadura al actor y expiración por inactividad
- [x] 1.5 Rechazar el hilo de otro actor sin exponer ninguno de sus turnos
- [x] 1.6 Un identificador inexistente o vencido devuelve hilo nuevo, no error
- [x] 1.7 `OpcionesAsistente` — vigencia del hilo y tope de turnos del historial
- [x] 1.8 Test: el actor dueño lo recupera y un tercero no
- [x] 1.9 Test: expira por inactividad y un turno nuevo lo renueva
- [x] 1.10 Test: el recorte ancla el inicio de segmento y respeta el tope
- [x] 1.11 Test: el hilo no contiene ningún valor de ninguna fila después de un turno con datos

## 2. Enrutador de intención social y meta (ARS-39)

- [x] 2.1 `Application/EnrutadorSocial.cs` — clase pura, sin base ni red
- [x] 2.2 Clasificar saludo y agradecimiento por **ausencia de contenido** tras quitar la apertura social
- [x] 2.3 Clasificar la meta-pregunta por patrón angosto que nombre al asistente
- [x] 2.4 Respuestas fijas en español, sin prometer nada que el sistema no haga
- [x] 2.5 Test: «hola», «gracias» y la meta-pregunta resuelven con cero llamadas al modelo
- [x] 2.6 Test: «hola, ¿cuántos docentes tiene Inglés Nivel IV?» **no** se intercepta
- [x] 2.7 Test: «¿qué carreras hay?» no se toma por meta-pregunta
- [x] 2.8 Test: **ningún ítem del dataset de capacidad** se intercepta
- [x] 2.9 Verificar en rojo: sacar la guarda de contenido hace que el saludo con pregunta se intercepte

## 3. Índice de entidades (ARS-40, primera mitad)

- [x] 3.1 `Application/IIndiceDeEntidades.cs` — contrato compartido por el detector de ambigüedad y el de cambio de tema
- [x] 3.2 `Infrastructure/IndiceDeEntidades.cs` — cargar materias con su carrera y personas con su apellido, de la base
- [x] 3.3 Cachear con la misma pereza que el prefijo del prompt, y exponer un contador de lecturas para el test del caché
- [x] 3.4 Detectar las colisiones: nombre de materia en más de una carrera, apellido en más de una persona
- [x] 3.5 Test: el índice se carga de la base y no tiene valores embebidos
- [x] 3.6 Test: dos turnos consecutivos leen la base una sola vez

## 4. Detector de ambigüedad (ARS-40, segunda mitad)

- [x] 4.1 `Application/DetectorDeAmbiguedad.cs` — busca en la pregunta términos del índice que colisionan
- [x] 4.2 No disparar si la pregunta ya trae el discriminador
- [x] 4.3 Disparar **solo** ante colisión verificada, nunca por vaguedad
- [x] 4.4 `Application/Aclaracion.cs` — la aclaración pendiente y sus opciones con etiqueta canónica
- [x] 4.5 Test contra base real: materia repetida devuelve las carreras, con cero tokens
- [x] 4.6 Test contra base real: apellido compartido devuelve las personas con nombre completo
- [x] 4.7 Test: con el discriminador presente no se pide aclaración
- [x] 4.8 Test: una materia sin colisión no dispara
- [x] 4.9 Test: una pregunta vaga sin colisión no dispara
- [x] 4.10 Test: el generador del fixture declara las colisiones que el detector necesita

## 5. Reconocedor de la respuesta a una aclaración (ARS-41)

- [x] 5.1 `Application/ReconocedorDeAclaracion.cs` — clase pura
- [x] 5.2 Tres pasos en orden: etiqueta completa, token distintivo, ordinal
- [x] 5.3 Un empate entre dos opciones vuelve a preguntar en vez de elegir
- [x] 5.4 Máquina de intentos con tope y salida definida
- [x] 5.5 Devolver la etiqueta canónica de la opción, nunca el texto del usuario
- [x] 5.6 Test: los tres pasos reconocen
- [x] 5.7 Test: un empate no resuelve y reofrece
- [x] 5.8 Test: al agotar los intentos la aclaración se abandona y no queda pendiente
- [x] 5.9 Test: un ordinal llega al reescritor convertido en etiqueta

## 6. Detector de cambio de tema (ARS-43)

- [x] 6.1 `Application/DetectorDeCambioDeTema.cs` — clase pura
- [x] 6.2 Marcar pivote solo si **no** hay marcador anafórico **y** hay un término del índice que no está activo
- [x] 6.3 Sin historial no hay pivote
- [x] 6.4 Al marcarlo, mover el inicio de segmento del hilo
- [x] 6.5 Test EN MEMORIA Y PRIMERO: «¿y en Sistemas?» no marca pivote
- [x] 6.6 Test: otra entidad sin anáfora sí lo marca
- [x] 6.7 Test: el inicio de segmento se mueve y el historial vigente queda vacío
- [x] 6.8 Verificar en rojo: sacar la guarda anafórica rompe el caso canónico de seguimiento

## 7. Reescritor de preguntas de seguimiento (ARS-42)

- [x] 7.1 `Application/ReescritorDePreguntas.cs` — una llamada al modelo, temperatura cero
- [x] 7.2 Regla que enumera los campos del dominio y decide arrastrar o soltar **por campo**
- [x] 7.3 Prompt con un ejemplo que arrastra y uno que descarta
- [x] 7.4 No llamar cuando el historial vigente está vacío
- [x] 7.5 Ante una reescritura vacía o absurda, conservar el mensaje original
- [x] 7.6 Test: sin historial no hay llamada; con historial hay exactamente una
- [x] 7.7 Test: el prompt nombra los campos uno por uno y trae los dos ejemplos
- [x] 7.8 Test: ningún ejemplo del prompt coincide con un ítem del dataset de capacidad
- [x] 7.9 Test: en el pivote, lo que se le manda al modelo no contiene ningún turno anterior

## 8. Orquestador de la capa

- [x] 8.1 `Application/CapaConversacional.cs` — compone las piezas en el orden del diseño y delega en el carril SQL
- [x] 8.2 Saltear el enrutador social por completo cuando hay aclaración pendiente
- [x] 8.3 `ResultadoDelTurno` — identificador del hilo y opciones de la aclaración
- [x] 8.4 Devolver siempre la pregunta interpretada en el turno de pivote
- [x] 8.5 Registrar la pieza y sus dependencias en `ModuleExtensions`
- [x] 8.6 Test de punta a punta: saludo, pregunta, seguimiento, aclaración y pivote en un mismo hilo
- [x] 8.7 Test: sin hilo, una pregunta autocontenida se responde igual

## 9. Documentación en el mismo cambio

- [x] 9.1 `docs/architecture/domains/asistente.md` — la capa, su orden y por qué el pivote se fuerza
- [x] 9.2 `backend/src/Modules.Asistente/README.md` — el diagrama del turno con la capa arriba
- [x] 9.3 Registrar en el PR los tres criterios que no se pueden verificar sin eje conversacional ni proveedor real
