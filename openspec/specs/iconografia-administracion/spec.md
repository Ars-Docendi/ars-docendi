# iconografia-administracion Specification

## Purpose

Establece una iconografía administrativa coherente, legible y distinguible para que Usuarios, Docentes, Roles y Membresía de Roles se reconozcan rápidamente.

## Requirements

### Requirement: Iconos administrativos distinguibles

El sidebar SHALL mostrar un icono visual distinto y semánticamente reconocible para cada entrada: personas agrupadas para Usuarios, educación para Docentes, protección o rol para Roles y permisos protegidos para Membresía de Roles.

#### Scenario: Iconos de configuración

- **WHEN** Secretaría o Administración visualiza el grupo Configuración
- **THEN** Usuarios, Docentes, Roles y Membresía de Roles presentan iconos diferentes y no ambiguos

### Requirement: Estilo visual consistente

Los cuatro iconos SHALL compartir viewBox, grosor de trazo, terminaciones, color heredado y tamaño visual, y MUST permanecer legibles en el sidebar expandido y colapsado.

#### Scenario: Sidebar colapsado

- **WHEN** el sidebar se muestra sólo con iconos
- **THEN** cada entrada conserva su forma reconocible y el texto accesible continúa disponible mediante el nombre o tooltip del enlace

### Requirement: No depender sólo del icono

Los enlaces SHALL conservar un nombre textual visible o accesible; ningún icono administrativo crítico DEBE ser la única forma de identificar la acción.

#### Scenario: Navegación accesible

- **WHEN** un lector de pantalla recorre el sidebar
- **THEN** cada enlace se anuncia con su nombre en español independientemente del SVG decorativo
