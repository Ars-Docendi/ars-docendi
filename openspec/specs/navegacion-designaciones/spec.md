# navegacion-designaciones Specification

## Purpose

Organizar las pantallas del circuito docente bajo un sector propio en la navegación lateral, conservando el acceso correspondiente a cada rol y la navegación accesible.

## Requirements

### Requirement: Sector DESIGNACIONES sin enlace redundante

La sidebar SHALL agrupar las pantallas autorizadas bajo el encabezado DESIGNACIONES, al mismo nivel que TRABAJO y CONFIGURACION. MUST NOT mostrar un enlace padre Designaciones ni duplicar sus pantallas en otro sector. Jefe SHALL ver Mis pedidos; Coordinador, Secretaría, Decanato y Administrativo SHALL ver Revisión; Secretaría SHALL conservar Períodos. Los roles sin pantallas autorizadas MUST NOT ver un sector vacío.

#### Scenario: Secretaría abre la navegación

- **GIVEN** un usuario Secretaría
- **WHEN** se muestra la sidebar
- **THEN** MUST ver DESIGNACIONES con Revisión y Períodos, sin botón padre Designaciones

#### Scenario: Sidebar contraída

- **GIVEN** cualquiera de los roles con pantallas de Designaciones
- **WHEN** contrae la sidebar y navega con teclado
- **THEN** MUST conservar acceso a cada pantalla autorizada con nombre accesible y foco visible

#### Scenario: Rol sin pantallas de Designaciones

- **GIVEN** un Docente sin rol revisor o Jefe
- **WHEN** se muestra la sidebar
- **THEN** MUST NOT aparecer un encabezado DESIGNACIONES vacío ni enlaces no autorizados
