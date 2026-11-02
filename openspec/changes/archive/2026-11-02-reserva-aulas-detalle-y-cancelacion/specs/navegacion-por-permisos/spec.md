## MODIFIED Requirements

### Requirement: Navegación derivada de permisos

La sidebar SHALL calcular sus grupos y enlaces desde los permisos efectivos de la sesión mediante un registro de rutas propiedad del código. MUST ocultar los enlaces sin permiso, no mostrar grupos vacíos y conservar una etiqueta accesible para cada enlace. La selección de rutas NO SHALL depender del nombre ni del tipo institucional del rol. El permiso que habilita un enlace MUST ser el mismo que exige su ruta: un enlace NO SHALL mostrarse a quien la ruta redirigiría.

#### Scenario: Permiso habilita una pantalla

- **GIVEN** una sesión con el permiso requerido por una pantalla
- **WHEN** se renderiza la sidebar
- **THEN** aparece el enlace correspondiente dentro de su grupo

#### Scenario: Permiso revocado oculta una pantalla

- **GIVEN** una sesión sin el permiso requerido por una pantalla
- **WHEN** se renderiza la sidebar
- **THEN** el enlace no aparece y no queda un grupo vacío

#### Scenario: Rol personalizado con permisos de revisión

- **GIVEN** un rol personalizado con `designaciones.revisar`
- **WHEN** se renderiza la sidebar
- **THEN** aparece Revisión aunque el nombre del rol no sea institucional

#### Scenario: Reserva de aulas sólo para quien puede entrar

- **GIVEN** una sesión con `aulas.ver` pero sin `aulas.solicitar` ni `aulas.aprobar`
- **WHEN** se renderiza la sidebar
- **THEN** no aparece "Reserva de aulas", y con cualquiera de esos dos permisos sí aparece
