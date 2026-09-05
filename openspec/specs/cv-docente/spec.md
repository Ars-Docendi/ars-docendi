# cv-docente Specification

## Purpose

Permite al docente administrar un único CV en formato PDF dentro de su perfil profesional.

## Requirements

### Requirement: Carga del CV en PDF

El sistema SHALL permitir al docente cargar un CV en formato PDF como archivo **único** de su perfil. La sección CV sin archivo cargado MUST presentarse como una **zona de arrastre**, no como una fila con control de alta, de modo que la acción se entienda por la forma del control y sin texto explicativo. El archivo se registra como metadata mock (nombre y fecha de carga), sin storage real.

#### Scenario: Sección CV vacía

- **GIVEN** un docente que no cargó su CV
- **WHEN** abre `/portal`
- **THEN** la sección CV se presenta como zona de arrastre

#### Scenario: Cargar el CV

- **GIVEN** la sección CV vacía
- **WHEN** el docente carga un archivo PDF
- **THEN** la sección muestra el nombre del archivo y su fecha de carga

#### Scenario: Solo se acepta PDF

- **WHEN** el docente intenta cargar un archivo que no es PDF
- **THEN** el sistema rechaza el archivo e informa el formato admitido

### Requirement: Reemplazo y baja del CV

El sistema SHALL permitir al docente reemplazar el CV cargado por otro archivo y SHALL permitir eliminarlo. Al reemplazarlo, el archivo anterior MUST dejar de estar referenciado en el perfil: el CV es un único archivo, no un historial.

#### Scenario: Reemplazar el CV

- **GIVEN** un docente con un CV ya cargado
- **WHEN** carga un archivo nuevo
- **THEN** el perfil referencia solo el archivo nuevo con su fecha de carga actualizada

#### Scenario: Eliminar el CV

- **GIVEN** un docente con un CV cargado
- **WHEN** lo elimina
- **THEN** la sección CV vuelve a presentarse como zona de arrastre
