# Spec Delta

## Purpose

Define qué muestra la pantalla de ingreso de Ars Docendi: la identidad del sistema, los textos que guían el ingreso con Microsoft, el canal de ayuda y las condiciones de uso que se aceptan al ingresar.

## ADDED Requirements

### Requirement: Identidad del sistema en la pantalla de ingreso

La pantalla de ingreso SHALL mostrar el isotipo de Ars Docendi, una bienvenida al sistema y, en escritorio, su pertenencia al Departamento de Ingeniería e Investigaciones Tecnológicas de la UNLaM. El panel de bienvenida SHALL usar el verde de la paleta del sistema de diseño. La pestaña del navegador SHALL mostrar el isotipo de Ars Docendi como ícono.

#### Scenario: Bienvenida al abrir el ingreso

- **GIVEN** una persona sin sesión en un navegador de escritorio
- **WHEN** abre la pantalla de ingreso
- **THEN** ve el isotipo de Ars Docendi, el título "Te damos la bienvenida a Ars Docendi" y el nombre del departamento y de la universidad, sin el recuadro "UNLaM"

#### Scenario: Ícono de la pestaña

- **WHEN** una persona abre Ars Docendi en el navegador
- **THEN** la pestaña muestra el isotipo de Ars Docendi como ícono

### Requirement: Descripción de lo que ofrece el sistema

La pantalla SHALL describir el sistema enumerando designaciones, reservas de aula, tareas, perfil del docente y asistente conversacional.

#### Scenario: Descripción completa

- **GIVEN** una persona sin sesión
- **WHEN** abre la pantalla de ingreso
- **THEN** la descripción menciona designaciones, reservas de aula, tareas, perfil del docente y asistente conversacional

### Requirement: Textos de ingreso acordes al ingreso con Microsoft

El formulario SHALL titularse "Ingresá a tu cuenta" e indicar que se ingresa con la cuenta institucional @docentes.unlam.edu.ar. La acción principal SHALL rotularse "Ingresar con Microsoft". La indicación del dominio es orientativa: MUST NOT reemplazar ni agregar validaciones al ingreso con Microsoft.

#### Scenario: Acción principal

- **GIVEN** una persona sin sesión
- **WHEN** abre la pantalla de ingreso
- **THEN** ve el título "Ingresá a tu cuenta", la indicación de usar la cuenta @docentes.unlam.edu.ar y un botón "Ingresar con Microsoft"

### Requirement: Sin canales de contacto inexistentes

La pantalla MUST NOT mostrar direcciones de mail ni otros canales de contacto que no estén operativos, ni un bloque de ayuda permanente. El mensaje de error de autenticación SHALL indicar que, si el problema persiste, la persona se comunique con Secretaría Académica.

#### Scenario: Pantalla sin bloque de ayuda

- **GIVEN** una persona sin sesión
- **WHEN** abre la pantalla de ingreso
- **THEN** la pantalla no muestra un bloque de ayuda ni direcciones de mail

#### Scenario: Error de autenticación

- **GIVEN** que Microsoft devolvió un error de autenticación
- **WHEN** la persona vuelve a la pantalla de ingreso
- **THEN** el mensaje de error la invita a reintentar y, si persiste, a comunicarse con Secretaría Académica, sin mencionar un canal de soporte

### Requirement: Términos de uso y privacidad accesibles desde el ingreso

La pantalla de ingreso SHALL mostrar, en su pie, que al ingresar se aceptan los términos de uso y la política de privacidad, con un enlace a una página que los contiene. Esa página SHALL ser pública: se puede leer sin sesión.

#### Scenario: Enlace desde el pie del ingreso

- **GIVEN** una persona sin sesión
- **WHEN** abre la pantalla de ingreso y sigue el enlace del pie
- **THEN** llega a la página de términos de uso y privacidad sin que se le pida iniciar sesión

### Requirement: Contenido de los términos de uso y privacidad

La página SHALL informar, según la Ley 25.326: responsable de los datos, categorías de datos tratados, finalidad, carácter obligatorio y consecuencias, quién accede, medidas de seguridad y conservación, derechos de acceso, rectificación, actualización y supresión con el canal para ejercerlos, la leyenda del órgano de control y las obligaciones del usuario. SHALL indicar su fecha de última actualización.

#### Scenario: Derechos del titular y órgano de control

- **WHEN** una persona lee la página de términos
- **THEN** encuentra cómo ejercer sus derechos de acceso, rectificación, actualización y supresión, y la leyenda que identifica a la Agencia de Acceso a la Información Pública como órgano de control

#### Scenario: Datos tratados y finalidad

- **WHEN** una persona lee la página de términos
- **THEN** encuentra qué categorías de datos guarda el sistema, incluidas las imágenes de documentos, y que se usan sólo para la gestión académica del departamento

### Requirement: Pantalla de ingreso adaptable al dispositivo

La pantalla de ingreso SHALL poder usarse en escritorio, tablet y celular sin scroll horizontal. En un celular, la acción principal de ingreso SHALL quedar visible al abrir la pantalla, sin necesidad de desplazarse.

#### Scenario: Ingreso desde un celular

- **GIVEN** una persona sin sesión en un celular de 375 × 812 px
- **WHEN** abre la pantalla de ingreso
- **THEN** ve el botón "Ingresar con Microsoft" sin desplazarse y la página no tiene scroll horizontal
