## MODIFIED Requirements

### Requirement: El scope declarado por el rol gobierna la asignación a usuarios

El sistema SHALL validar que toda fila de `identity.user_roles` sea coherente con el rol asignado: un rol global MUST tener `materia_id` y `carrera_id` en NULL; un rol `docente` MUST tener `materia_id` y `carrera_id` presentes juntos, porque una materia dictada en más de una carrera exige elegir una; un rol `jefe_catedra` MUST tener `materia_id` presente y `carrera_id` en NULL; un rol `coordinador_carrera` MUST tener `carrera_id` presente y `materia_id` en NULL. `materia_id` referencia la materia canónica de `identity.materias`; `carrera_id` referencia `identity.carreras`. Esta validación SHALL aplicarse igual a los roles creados por el operador que a los de sistema, según el `scope` declarado por el rol.

#### Scenario: Asignación de un rol de materia sin carrera

- **GIVEN** un rol con `scope = 'materia'`
- **WHEN** se intenta asignarlo a un usuario sin `carrera_id`
- **THEN** el sistema MUST rechazar la asignación

#### Scenario: Asignación de Docente sin carrera

- **GIVEN** el rol de sistema `docente`
- **WHEN** se intenta asignarlo a un usuario con `materia_id` pero sin `carrera_id`
- **THEN** el sistema MUST rechazar la asignación, porque una materia compartida entre carreras exige elegir una

#### Scenario: Jefe de Cátedra con una sola membresía por materia

- **GIVEN** la materia `01032` se dicta en las carreras `201` y `202`
- **WHEN** se asigna el rol `jefe_catedra` sobre `01032`
- **THEN** el sistema MUST guardar una única fila con `materia_id` y sin `carrera_id`, válida en ambas carreras

#### Scenario: Asignación de un rol global con ámbito

- **GIVEN** un rol con `scope = 'global'`
- **WHEN** se intenta asignarlo a un usuario con `materia_id` o `carrera_id` cargados
- **THEN** el sistema MUST rechazar la asignación

#### Scenario: Un rol creado por el operador respeta su propio scope

- **GIVEN** un rol con `es_sistema = FALSE` y `scope = 'carrera'`
- **WHEN** se lo asigna a un usuario con `carrera_id` y sin `materia_id`
- **THEN** la asignación MUST aceptarse, con la misma validación que un rol de sistema

#### Scenario: Revocar y volver a otorgar la misma asignación

- **GIVEN** una asignación de rol previamente revocada (`deleted_at` no nulo)
- **WHEN** se otorga nuevamente el mismo `(usuario, rol, ámbito)`
- **THEN** la operación MUST aceptarse, porque el índice de unicidad solo alcanza a las asignaciones vivas
