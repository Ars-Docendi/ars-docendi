## MODIFIED Requirements

### Requirement: Edición explícita y atómica de membresías

Las pantallas administrativas MUST enviar la lista completa de membresías con sus IDs y ámbitos, y la API MUST reemplazarla atómicamente junto con los datos de identidad. Una asignación inválida o repetida MUST dejar intactos los datos y las membresías anteriores. Para una membresía de Docente, la API MUST validar que `materia_id` y `carrera_id` vengan juntos y que esa combinación se dicte de verdad (catálogo informativo materia–plan, vigente y activo). Para una membresía de Jefe de Cátedra, la API MUST validar que `materia_id` referencie una materia activa; no se envía carrera. Solo el rol de Coordinador lleva `carrera_id` solo.

#### Scenario: Cambio de rol entre materias

- **GIVEN** un usuario es `docente` en la materia/carrera A y `jefe_catedra` en la materia B
- **WHEN** el operador cambia la segunda membresía a la materia C y guarda
- **THEN** la consulta posterior conserva sólo `docente + A` y `jefe_catedra + C`

#### Scenario: Repetición de una misma asignación

- **GIVEN** el formulario envía dos veces el mismo `rolId + materiaId + carreraId`
- **WHEN** se intenta guardar
- **THEN** la API rechaza la operación como error de ámbito y no aplica cambios parciales

#### Scenario: Ámbito incompatible

- **GIVEN** un rol global recibe una materia o un rol de materia recibe una carrera incorrecta
- **WHEN** se intenta guardar
- **THEN** la API rechaza la operación y conserva las membresías anteriores

#### Scenario: Docente sin carrera

- **GIVEN** un rol `docente` recibe `materia_id` pero no `carrera_id`
- **WHEN** se intenta guardar la membresía
- **THEN** la API rechaza la operación y conserva las membresías anteriores

#### Scenario: Combinación materia–carrera que no se dicta

- **GIVEN** un rol `docente` referencia una materia y una carrera que no tienen pertenencia activa en el catálogo informativo materia–plan
- **WHEN** se intenta guardar la membresía
- **THEN** la API rechaza la operación y conserva las membresías anteriores
