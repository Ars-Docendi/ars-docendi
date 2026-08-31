## 1. Vocabulario del trámite leído de la base

- [ ] 1.1 `LectorDeVocabulario` — valores admitidos de un `CHECK`, desde `pg_constraint`
- [ ] 1.2 Exige la forma `columna = ANY (ARRAY[...])` y falla nombrando la restricción
- [ ] 1.3 Los cargos se leen de su tabla: nombre y abreviatura, solo los activos
- [ ] 1.4 Test: los estados leídos son exactamente los que declara la base
- [ ] 1.5 Test: novedades y tipos de baja, ídem
- [ ] 1.6 Test: los cargos traen nombre y abreviatura, y excluyen los inactivos
- [ ] 1.7 Test: una restricción sin literales falla nombrándola, y no devuelve vacío
- [ ] 1.8 Verificar en rojo: devolver lista vacía ante una forma inesperada hace fallar 1.7

## 2. El catálogo del dominio, extendido

- [ ] 2.1 `ClaseDeEntidad` suma las clases del trámite: estado, novedad, tipo de baja, cargo
- [ ] 2.2 `IIndiceDeEntidades` compone entidades y vocabulario en un solo catálogo
- [ ] 2.3 La carga sigue siendo perezosa y cacheada, como antes
- [ ] 2.4 Test: el `ping` responde con la base detenida
- [ ] 2.5 Test: dos turnos leen la base una sola vez
- [ ] 2.6 Test: el detector de ambigüedad y el de cambio de tema siguen viendo lo mismo que antes

## 3. El catálogo de intenciones

- [ ] 3.1 `Recursos/intenciones.json` — cinco intenciones con términos, slots y destino
- [ ] 3.2 `CatalogoDeIntenciones` — carga, valida y expone el catálogo
- [ ] 3.3 La validación falla nombrando la intención: campo vacío, clase de slot desconocida, término sin normalizar
- [ ] 3.4 El destino es una cadena lógica; nadie la invoca
- [ ] 3.5 Test: cada intención declara nombre, términos, slots y destino no vacíos
- [ ] 3.6 Test: una clase de slot inexistente no carga y nombra la intención
- [ ] 3.7 Test: un término acentuado o en mayúscula no carga
- [ ] 3.8 Test: el módulo sigue referenciando solo `ArsDocendi.Shared`

## 4. Reconocimiento y resolución

- [ ] 4.1 `ResolutorDeIntenciones` — reconoce por conjunto de términos sobre texto normalizado
- [ ] 4.2 Resuelve cada slot contra el catálogo del dominio
- [ ] 4.3 Un término que colisiona deja el slot sin resolver
- [ ] 4.4 Una intención con un slot sin resolver no queda reconocida
- [ ] 4.5 Test: reordenar las palabras reconoce la misma intención
- [ ] 4.6 Test: falta un término y no hay intención
- [ ] 4.7 Test: dos personas con el mismo apellido no resuelven el slot
- [ ] 4.8 Test: un apellido único sí resuelve
- [ ] 4.9 Test: un valor que no está en la base no resuelve
- [ ] 4.10 Test: el contador de llamadas al modelo queda en cero
- [ ] 4.11 Verificar en rojo: resolver la colisión al primer valor hace fallar 4.7

## 5. Cobertura por intención

- [ ] 5.1 Un caso por intención, con slots resueltos y sin resolver
- [ ] 5.2 Test: el catálogo se itera y falla nombrando la intención sin caso
- [ ] 5.3 Verificar en rojo: agregar una intención sin caso hace fallar 5.2

## 6. Documentación

- [ ] 6.1 `docs/architecture/domains/asistente.md` — el catálogo, su alcance y por qué no generaliza
- [ ] 6.2 Dejar escrito que el enrutador y los edges son el cambio siguiente
