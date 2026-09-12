## 0. Gate previo — decisión del cliente

> **No bloquea el merge del código: bloquea CONCEDER el permiso.** La máquina se
> puede desplegar apagada, y apagada no expone nada más que el perfil propio.

- [ ] 0.1 Obtener de Secretaría Académica la finalidad escrita del acceso, con los cuatro casos de uso del catálogo sobre la mesa (ARS-90)
- [ ] 0.2 Decirle a Secretaría, en esa misma conversación, que **contar y enumerar no se pueden separar**: quien pueda preguntar cuántos, puede preguntar quiénes
- [ ] 0.3 Acordar quién puede otorgar `portal.ver_trayectoria_ajena` y bajo qué criterio, que es la regla de control real (`BR-portal-002`)
- [ ] 0.4 Verificar las citas de la Ley 25.326 contra el texto vigente, y consultar con el área legal la inscripción del banco de datos ante la AAIP

## 1. Higiene previa — vale por sí sola aunque el gate diga que no

- [x] 1.1 Congelar las cuatro líneas de base del evaluador mientras los cassettes de entonces todavía sirven (ARS-84)
- [x] 1.2 Enchufar el gate de regresión, que estaba entero y no lo invocaba nadie
- [x] 1.3 Verificar que el gate PUEDE fallar, mutando un ítem de la línea de base
- [x] 1.4 `PerfilDelActor.AlcanzaTodo`: el ámbito deja de ser proxy de «ve todo» (ARS-86)
- [x] 1.5 Test rojo primero: un actor global sin `designaciones.ver` no alcanza todo
- [x] 1.6 Cuarta dirección del manifiesto: todo schema de la base tiene que estar clasificado (ARS-87)
- [x] 1.7 FK `portal.perfiles.persona_id → identity.personas.id`, sin `ON DELETE` (ARS-88)
- [x] 1.8 Derivar del manifiesto la lista de schemas de `PrefijoDeEsquemaTests`, que estaba clavada (ARS-89)
- [x] 1.9 Escribir el invariante #14 en `CLAUDE.md`, marcado como pendiente de acuerdo (ARS-85) — ratificado el 2026-09-08; la marca ya no está

## 2. El catálogo de preguntas, que dimensiona todo lo demás

- [x] 2.1 Escribir en `domains/asistente.md` qué se puede preguntar sobre portal, y qué NO (ARS-101)
- [x] 2.2 Derivar el alcance de tablas hacia atrás desde las preguntas: seis, no nueve
- [x] 2.3 Registrar la limitación de contar-versus-enumerar como declarada y no como nota al pie

## 3. La frontera en el motor

- [x] 3.1 Permiso `portal.ver_trayectoria_ajena`, concedido a nadie, con la guarda de rol de sistema (ARS-91)
- [x] 3.2 `identity.asistente_persona()`, quinta función SECURITY DEFINER, que devuelve nulo sin romper
- [x] 3.3 `GRANT EXECUTE` de la función a los dos roles: `CREATE`, `REVOKE FROM PUBLIC` y `GRANT` son una unidad
- [x] 3.4 RLS sobre las seis tablas, con el predicado disyuntivo y sin `asistente_es_global()` (ARS-92)
- [x] 3.5 Cada policy nombra al actor y califica toda referencia a la fila externa
- [x] 3.6 Tests de frontera con un rol descartable, `NOSUPERUSER NOBYPASSRLS`
- [x] 3.7 `COMMENT ON` de las seis tablas y sus columnas concedidas (ARS-93)
- [x] 3.8 Decir en el comentario de `termino_norm` que la normalización es a MAYÚSCULAS
- [x] 3.9 `GRANT` columna por columna, y las dos entradas de los manifiestos (ARS-94)
- [x] 3.10 Denegar `contactos`, `cvs`, `proyectos` y `proyecto_documentos` con motivo escrito
- [x] 3.11 Test con los roles REALES: con el `GRANT` puesto, nadie ve un perfil ajeno

## 4. Que el vacío no se lea como un hecho

- [x] 4.1 `CoberturaDelPortal`: qué tablas toca la consulta y cómo se dice la cobertura (ARS-95)
- [x] 4.2 `IConsultorDeCobertura` y su implementación, con la conexión del actor
- [x] 4.3 Sumar la cobertura al texto del resultado vacío, sin reemplazar el aviso de alcance
- [x] 4.4 Regla de redacción cuando hay filas, que es lo único que restringe una narración
- [x] 4.5 Un fallo del conteo no puede tumbar el turno

## 5. La superficie visible

- [x] 5.1 `PerfilDelActor.VeTrayectoriaAjena`, leído en vivo (ARS-97)
- [x] 5.2 La presentación anuncia según el permiso y no según el rol
- [x] 5.3 Test: nunca se promete el contacto ni el CV
- [x] 5.4 Cuatro ejemplos de portal, disjuntos de los ítems de evaluación por diseño
- [x] 5.5 Test nuevo: cada ejemplo del catálogo ejecuta y devuelve filas
- [x] 5.6 El frontend NO se toca

## 6. Normativa y documentación

- [x] 6.1 `docs/business-rules/portal.md` con BR-001..006 (ARS-96)
- [x] 6.2 `docs/business-rules/asistente.md` con BR-001..004
- [x] 6.3 Advertencia arriba de todo: las citas de la ley están pendientes de verificación
- [x] 6.4 Regenerar el índice de business rules
- [x] 6.5 Docs de arquitectura en el mismo PR (ARS-100)

## 7. Medición

- [x] 7.1 Sembrar portal en `GeneradorDeFixture`, con cardinalidades declaradas (ARS-98)
- [x] 7.2 Que el fixture conceda el permiso: sin eso los ítems dan verde sin consultar el padrón
- [x] 7.3 Ocho ítems: cuatro del catálogo, uno de agregación y tres infactibles
- [x] 7.4 Extender la tabla dorada del enrutador a los ítems nuevos
- [x] 7.5 Corrida financiada: regrabar el corpus y medir portal (ARS-99)
- [x] 7.6 Archivar las líneas de base de antes con el detalle de qué costó el cambio
- [x] 7.7 Congelar las nuevas y verificar que el gate vuelve a comparar
