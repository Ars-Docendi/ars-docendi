---
status: review
owner: "Equipo Ars Docendi"
feature: "openspec/changes/modelo-datos-identity-designaciones/specs/pedidos-designacion/spec.md"
last_updated: 2026-08-18
---

# Design spec: Bloqueo de pedido duplicado entre cátedras

## Resumen

Se diseña la respuesta que recibe un **Jefe de Cátedra** cuando intenta iniciar un pedido para un
docente que ya tiene otro trámite no terminal en el período. BR-designaciones-001 aplica aunque el
pedido previo pertenezca a otra cátedra: la interfaz debe explicar el bloqueo de forma accionable sin
revelar la materia, el contenido, el estado detallado ni el autor del trámite bloqueante.

## Roles que ven esta surface

- [x] Jefe de Cátedra
- [ ] Coordinador de Carrera
- [ ] Secretaría Académica
- [ ] Decanato
- [ ] Administrativos
- [ ] Docente

## Flujo principal

1. El Jefe de Cátedra entra a `/designaciones/pedidos/nuevo` desde su cátedra y selecciona o carga al
   docente.
2. Al intentar guardar o enviar, el frontend prevalida los pedidos visibles y el backend valida todos
   los pedidos vivos del período. PostgreSQL conserva la autoridad final ante escrituras concurrentes.
3. Si ya existe un pedido no terminal, la creación no se completa y el formulario conserva los datos
   ingresados.
4. El campo o sección del docente recibe el mensaje: **“Ya existe un pedido en curso para este docente
   en el período.”**
5. El Jefe de Cátedra puede corregir la selección o volver a “Mis pedidos”. No se ofrece navegar al
   pedido bloqueante porque podría pertenecer a una cátedra fuera de su ámbito.

## Layout / IA

- El error se muestra junto al selector o bloque de datos del docente, que es el dato que causa el
  conflicto. No se usa un modal: el usuario debe conservar el contexto y poder corregir la selección.
- Si la respuesta del backend llega después de una validación local satisfactoria —incluida la carrera
  concurrente entre dos requests— se presenta el mismo mensaje y en el mismo lugar.
- El foco se mueve al bloque del docente y el mensaje queda asociado al control para lectores de
  pantalla mediante el mecanismo de error de `Field`.
- El resto de los valores cargados permanece intacto. El usuario no debe reconstruir un formulario que
  la base rechazó por una condición externa.

## Estados a diseñar

| Estado              | Descripción                                                   | Cuándo se muestra                                       |
| ------------------- | ------------------------------------------------------------- | ------------------------------------------------------- |
| Validando           | La acción de guardar/enviar queda temporalmente deshabilitada | Mientras se consulta o persiste el pedido               |
| Sin conflicto       | El formulario continúa con su validación normal               | No existe otro pedido vivo para la persona y el período |
| Bloqueado           | Error inline con el mensaje seguro de BR-designaciones-001    | Existe un pedido no terminal, incluso en otra cátedra   |
| Carrera concurrente | Mismo estado bloqueado, sin mensaje técnico de PostgreSQL     | Otra escritura ganó después de la prevalidación         |
| Reintento permitido | La creación continúa normalmente                              | El pedido anterior está `rechazado` o `cancelado`       |

## Copy aprobado

**Mensaje principal:** “Ya existe un pedido en curso para este docente en el período.”

El texto comunica qué impide continuar y el alcance temporal de la regla. No afirma quién creó el
pedido ni dónde se tramita. El frontend puede usar “este docente”; el backend usa la variante
equivalente “ese docente”. Ambos deben conservar el mismo contenido informativo.

## Decisiones de diseño

- **Mensaje idéntico en prevalidación y conflicto de base.** El usuario no necesita distinguir si el
  bloqueo se detectó antes del `INSERT` o mediante el índice único; esa diferencia es interna.
- **Sin enlace al pedido bloqueante.** La existencia del trámite puede informarse porque impide la
  acción, pero revelar su detalle violaría el ámbito del segundo Jefe de Cátedra.
- **Sin identificar la cátedra.** Incluso un rótulo de materia permitiría inferir qué equipo inició el
  trámite. La regla se explica sólo a nivel docente/período.
- **Datos del formulario preservados.** El bloqueo puede resolverse por un cambio externo; descartar la
  carga no aporta seguridad y aumenta el costo de recuperación.
- **Los estados terminales liberan el cupo.** Un pedido `rechazado` o `cancelado` no dispara este estado,
  alineado con el índice parcial y con la posibilidad de volver a presentar.

## Anti-patterns a evitar

- Mostrar número, cátedra, materia, novedad, etapa, comentario, contenido o autor del pedido
  bloqueante.
- Incluir IDs, nombre del índice, SQLSTATE o texto de excepción de PostgreSQL.
- Redirigir automáticamente al detalle de un pedido que el actor quizá no pueda consultar.
- Afirmar que el docente “ya tiene un pedido en esta cátedra”: BR-designaciones-001 es global al
  período, no por materia.
- Borrar los campos ya completados o cerrar el formulario al recibir el conflicto.

## Referencias

- [Principios de diseño](../design-principles.md)
- [Delta spec de pedidos](../../../openspec/changes/modelo-datos-identity-designaciones/specs/pedidos-designacion/spec.md)
- [Persistencia de designaciones](../../../openspec/changes/modelo-datos-identity-designaciones/specs/persistencia-designaciones/spec.md)
- [BR-designaciones-001](../../business-rules/designaciones.md)
- [Design spec del flujo de pedidos](./proyecto-docente-design-spec.md)

## Open questions de diseño

- Si el cliente cambia BR-designaciones-001 para que la unicidad sea por cátedra, este estado sólo
  aplicará a duplicados dentro de la misma materia y deberá revisarse el copy.
