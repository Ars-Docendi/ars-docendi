## ADDED Requirements

### Requirement: Fecha de nacimiento sin icono SVG adicional

El campo "Fecha de nacimiento" del modal "Editar usuario" SHALL usar el control nativo de fecha
del navegador y MUST NOT renderizar un SVG de calendario separado dentro del campo. El valor, la
edición y la validación del campo SHALL conservar el comportamiento existente.

#### Scenario: Apertura del campo de fecha

- **GIVEN** el operador abre "Editar usuario" para una cuenta con fecha de nacimiento
- **WHEN** el modal se muestra
- **THEN** el campo contiene la fecha actual y no existe un elemento SVG de calendario adicional
  asociado al campo

#### Scenario: Guardado de la fecha

- **GIVEN** el operador modifica una fecha válida
- **WHEN** confirma "Guardar cambios"
- **THEN** el formulario envía la fecha modificada con el mismo formato y junto con el resto de
  los datos del usuario
