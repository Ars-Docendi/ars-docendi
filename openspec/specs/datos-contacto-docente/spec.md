# datos-contacto-docente Specification

## Purpose

Permite al docente mantener sus datos de contacto de forma independiente y opcional.

## Requirements

### Requirement: Edición de los datos de contacto

El sistema SHALL permitir al docente editar su **teléfono** y su **mail** de contacto desde la sección Contacto de su Portal. La edición MUST hacerse en línea dentro de la sección, sin abrir un diálogo. Los valores se persisten en el store mock local. Ambos campos MUST poder quedar vacíos: no son obligatorios.

#### Scenario: Cargar el teléfono y el mail

- **GIVEN** un docente en la sección Contacto de su Portal
- **WHEN** edita el teléfono y el mail y confirma
- **THEN** los valores quedan guardados y la sección vuelve a modo lectura mostrándolos

#### Scenario: Edición en línea

- **WHEN** el docente inicia la edición de Contacto
- **THEN** los campos se editan dentro de la propia sección, sin abrir un diálogo

#### Scenario: Guardar con un solo campo cargado

- **GIVEN** un docente que solo quiere cargar su teléfono
- **WHEN** completa el teléfono, deja el mail vacío y confirma
- **THEN** el cambio se guarda sin exigir el mail

#### Scenario: Descartar la edición

- **GIVEN** la sección Contacto en edición con cambios sin confirmar
- **WHEN** el docente cancela
- **THEN** los valores previos se conservan

### Requirement: Validación del formato del mail de contacto

El sistema SHALL validar que el mail de contacto tenga formato de dirección de correo válida antes de guardarlo. Si el formato es inválido, el sistema MUST señalar el error en el propio campo y MUST NOT guardar la sección.

#### Scenario: Mail con formato inválido

- **GIVEN** el docente editando su Contacto
- **WHEN** ingresa un mail con formato inválido y confirma
- **THEN** el campo muestra el error y la sección no se guarda

#### Scenario: Mail con formato válido

- **WHEN** el docente ingresa un mail con formato válido y confirma
- **THEN** el mail se guarda sin error
