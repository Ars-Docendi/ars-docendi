## 1. Vocabulario del trámite leído de la base

- [x] 1.1 `LectorDeVocabulario` — valores admitidos de un `CHECK`, desde `pg_constraint`
- [x] 1.2 Exige la forma `columna = ANY (ARRAY[...])` y falla nombrando la restricción
- [x] 1.3 Los cargos se leen de su tabla: nombre y abreviatura, solo los activos
- [x] 1.4 Test: los estados leídos son exactamente los que declara la base
- [x] 1.5 Test: novedades y tipos de baja, ídem
- [x] 1.6 Test: los cargos traen nombre y abreviatura, y excluyen los inactivos
- [x] 1.7 Test: una restricción sin literales falla nombrándola, y no devuelve vacío
- [x] 1.8 Verificar en rojo: devolver lista vacía ante una forma inesperada hace fallar 1.7

## 2. El catálogo del dominio, compuesto al lado del índice

- [x] 2.1 `ClaseDeSlot` — las clases del trámite: estado, novedad, tipo de baja, cargo, más materia y persona
- [x] 2.2 `ICatalogoDelDominio` compone el índice de entidades y el vocabulario, sin tocar ninguno
- [x] 2.3 `CatalogoDeEntidades` queda intacto: los dos detectores que lo consumen no cambian
- [x] 2.4 La carga es perezosa y cacheada, con el mismo patrón que el índice
- [x] 2.5 Test: el `ping` responde con la base detenida
- [x] 2.6 Test: dos turnos leen la base una sola vez
- [x] 2.7 Test: el vocabulario del trámite NO aparece en el índice de entidades

## 3. El catálogo de intenciones

- [x] 3.1 `Recursos/intenciones.json` — cinco intenciones con términos, slots y destino
- [x] 3.2 `CatalogoDeIntenciones` — carga, valida y expone el catálogo
- [x] 3.3 La validación falla nombrando la intención: campo vacío, clase de slot desconocida, término sin normalizar
- [x] 3.4 El destino es una cadena lógica; nadie la invoca
- [x] 3.5 Test: cada intención declara nombre, términos, slots y destino no vacíos
- [x] 3.6 Test: una clase de slot inexistente no carga y nombra la intención
- [x] 3.7 Test: un término acentuado o en mayúscula no carga
- [x] 3.8 Test: el módulo sigue referenciando solo `ArsDocendi.Shared`

## 4. Reconocimiento y resolución

- [x] 4.1 `ResolutorDeIntenciones` — reconoce por conjunto de términos sobre texto normalizado
- [x] 4.2 Resuelve cada slot contra el catálogo del dominio
- [x] 4.3 Un término que colisiona deja el slot sin resolver
- [x] 4.4 Una intención con un slot sin resolver no queda reconocida
- [x] 4.5 Test: reordenar las palabras reconoce la misma intención
- [x] 4.6 Test: falta un término y no hay intención
- [x] 4.7 Test: dos personas con el mismo apellido no resuelven el slot
- [x] 4.8 Test: un apellido único sí resuelve
- [x] 4.9 Test: un valor que no está en la base no resuelve
- [x] 4.10 Test: el contador de llamadas al modelo queda en cero
- [x] 4.11 Verificar en rojo: resolver la colisión al primer valor hace fallar 4.7

## 5. Cobertura por intención

- [x] 5.1 Un caso por intención, con slots resueltos y sin resolver
- [x] 5.2 Test: el catálogo se itera y falla nombrando la intención sin caso
- [x] 5.3 Verificar en rojo: agregar una intención sin caso hace fallar 5.2

## 6. Documentación

- [x] 6.1 `docs/architecture/domains/asistente.md` — el catálogo, su alcance y por qué no generaliza
- [x] 6.2 Dejar escrito que el enrutador y los edges son el cambio siguiente
