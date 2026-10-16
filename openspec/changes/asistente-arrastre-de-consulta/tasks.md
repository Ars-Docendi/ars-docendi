# Tareas

## 1. La enmienda a D1

- [x] 1.1 Reescribir D1 de `openspec/changes/asistente-capa-conversacional/design.md` nombrando qué se suma al hilo y por qué la propiedad que protege sigue en pie. No reinterpretar el texto vigente: hoy dice «nada más».
- [x] 1.2 Actualizar el diagrama de `Modules.Asistente/README.md`, que dice «guarda PREGUNTAS, nunca filas».

## 2. El hilo

- [x] 2.1 `TurnoDelHilo` pasa a `(Pregunta, SqlEjecutado, Cuando)`, con `SqlEjecutado` anulable.
- [x] 2.2 `HiloConversacional.Agregar` acepta la consulta; los turnos sin filas, abstenidos y degradados la pasan nula.
- [x] 2.3 Test: un turno respondido anota su consulta; uno vacío no.
- [x] 2.4 Test: **el hilo nunca contiene un valor de una fila**. Es el que cuida la propiedad de D1.

## 3. Qué consulta se anota

- [x] 3.1 `ResultadoDelTurno` expone la consulta ejecutada de forma independiente del campo `Sql`, que sigue gobernado por `asistente.ver_consulta` — el arrastre es interno y no puede heredar la visibilidad del usuario.
- [x] 3.2 Con reintento se anota la segunda consulta, que es la que respondió.
- [x] 3.3 Test del reintento: la anotada es la que devolvió filas.

## 4. El prompt

- [x] 4.1 Regla del seguimiento en `RenderizadorDeEsquema.Instrucciones`: editar o anidar la consulta anterior en vez de rehacerla.
- [x] 4.2 Las consultas arrastradas se escriben en `GeneradorDeSql.ArmarMensaje`, que es la parte variable.
- [x] 4.3 Test: dos turnos del mismo hilo producen huellas de prefijo idénticas.
- [x] 4.4 Test: el mensaje de generación de un seguimiento contiene la consulta anterior; el de un primer turno no.

## 5. El recorte

- [x] 5.1 El arrastre toma del segmento vigente y respeta `TopeDeTurnosDelHistorial`.
- [x] 5.2 Test del pivote: al soltar el tema no viaja ninguna consulta anterior. Se verifica sobre **qué se le mandó al modelo**, no sobre su salida, igual que el test de D3 de la capa conversacional.

## 6. De punta a punta

- [x] 6.1 Test de integración con proveedor guionado: turno 1 devuelve materias, turno 2 pregunta «los profesores de esa materia» y la generación recibe la consulta del turno 1.
- [x] 6.2 Un ejemplo del caso en el dataset de evaluación, en el eje de diálogo.

## 6b. La red de seguridad: el seguimiento que igual no se resuelve

- [x] 6b.1 `PoliticaDeAbstencion.HayReferenciaSinResolver` con lista propia de demostrativos. **No reusar la de `DetectorDeCambioDeTema`**: incluye artículos a propósito y marcaría casi toda frase.
- [x] 6b.2 Texto propio, que no afirma nada sobre los datos.
- [x] 6b.3 Las tres condiciones en `CapaConversacional`: hubo historial, quedó demostrativo, y el turno se rechazó.
- [x] 6b.4 Tests de las tres condiciones, incluida la que evita la explicación falsa.

## 7. Medición

- [ ] 7.1 Regrabar el corpus de cassettes: la regla nueva cambia el prefijo.
- [ ] 7.2 Corrida financiada y comparación contra la línea de base. **Requiere autorización de gasto explícita.**
- [ ] 7.3 Actualizar `docs/quality/scorecard.md` con los cuatro ejes.
