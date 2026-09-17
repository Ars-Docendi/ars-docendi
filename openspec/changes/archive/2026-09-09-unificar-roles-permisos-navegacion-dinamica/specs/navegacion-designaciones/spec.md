## MODIFIED Requirements

### Requirement: Sector DESIGNACIONES sin enlace redundante

La sidebar SHALL agrupar las pantallas autorizadas bajo el encabezado DESIGNACIONES, al mismo nivel que TRABAJO y CONFIGURACION. MUST NOT mostrar un enlace padre Designaciones ni duplicar sus pantallas en otro sector. Cada ítem SHALL depender del permiso efectivo de su pantalla: Revisión de `designaciones.revisar`, Períodos de `periodos.administrar` y Mis pedidos del permiso de consulta correspondiente. Los roles institucionales son sólo el preset inicial de esos permisos; los roles personalizados también pueden ver los ítems. Los roles sin pantallas autorizadas MUST NOT ver un sector vacío.

#### Scenario: Permisos institucionales por defecto

- **GIVEN** un usuario institucional con los permisos predeterminados para Revisión o Períodos
- **WHEN** se muestra la sidebar
- **THEN** ve únicamente las pantallas correspondientes a sus permisos, sin enlace padre ni duplicados

#### Scenario: Secretaría abre la navegación

- **GIVEN** un usuario Secretaría con los permisos predeterminados de Revisión y Períodos
- **WHEN** se muestra la sidebar
- **THEN** MUST ver DESIGNACIONES con Revisión y Períodos, sin botón padre Designaciones

#### Scenario: Rol personalizado habilita Revisión

- **GIVEN** un usuario con un rol personalizado que tiene `designaciones.revisar`
- **WHEN** se muestra la sidebar
- **THEN** ve Revisión aunque no tenga un nombre de rol institucional

#### Scenario: Permiso revocado quita la pantalla

- **GIVEN** un usuario cuyo rol ya no tiene el permiso de una pantalla de Designaciones
- **WHEN** vuelve a validar su sesión
- **THEN** el ítem desaparece y el guard de la ruta también bloquea el acceso directo

#### Scenario: Sidebar contraída

- **GIVEN** un usuario con al menos un permiso de Designaciones
- **WHEN** contrae la sidebar y navega con teclado
- **THEN** MUST conservar acceso a cada pantalla autorizada con nombre accesible y foco visible

#### Scenario: Rol sin pantallas de Designaciones

- **GIVEN** un usuario sin ninguno de los permisos de Designaciones
- **WHEN** se muestra la sidebar
- **THEN** MUST NOT aparecer un encabezado DESIGNACIONES vacío ni enlaces no autorizados
