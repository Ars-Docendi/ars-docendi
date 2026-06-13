## ADDED Requirements

### Requirement: Formulario de una sola página con TOC y footer sticky

El sistema SHALL presentar la creación de un Pedido de Designación como un formulario de una sola página con una tabla de contenidos (TOC) de secciones pegada a la izquierda y un footer pegado al pie que contiene la única acción primaria de la pantalla.

El TOC SHALL listar las 5 secciones en orden (1. Tipo de pedido, 2. Datos del docente, 3. Designación, 4. Justificación, 5. Documentación) e indicar el estado de cada sección: completada (`✓`), con error (`!`) o actual.

El footer SHALL mostrar a la izquierda el mensaje de estado de validación y a la derecha las acciones Cancelar, Guardar borrador y Enviar a revisión, con "Enviar a revisión" como única acción primaria (variante `primary`).

#### Scenario: Render del formulario con sus 5 secciones

- **WHEN** el usuario abre la pantalla de crear un Pedido de Designación
- **THEN** se muestran las secciones 1 a 5 en la columna de contenido, el TOC sticky a la izquierda con las 5 entradas, y el footer sticky con las acciones Cancelar / Guardar borrador / Enviar a revisión

#### Scenario: El TOC refleja el estado de cada sección

- **WHEN** una sección está completa y otra tiene un campo obligatorio sin completar
- **THEN** el TOC marca la sección completa con `✓` y la sección incompleta con `!`

### Requirement: Selección del tipo de pedido por tarjetas

El sistema SHALL permitir elegir el tipo de pedido entre cuatro opciones presentadas como tarjetas seleccionables: Alta nueva, Renovación, Cambio de cargo y Baja. La selección SHALL ser única (semántica de radiogroup) y la tarjeta seleccionada SHALL distinguirse visualmente. La tarjeta "Alta nueva" SHALL exhibir el indicador "requiere CV + DNI" antes de ser seleccionada.

#### Scenario: Seleccionar un tipo de pedido

- **WHEN** el usuario hace click en la tarjeta "Renovación"
- **THEN** la tarjeta "Renovación" queda marcada como seleccionada y cualquier otra tarjeta previamente seleccionada se deselecciona

#### Scenario: La tarjeta Alta nueva anticipa el requisito documental

- **WHEN** se muestra la grilla de tipos de pedido
- **THEN** la tarjeta "Alta nueva" muestra el flag "requiere CV + DNI" sin necesidad de seleccionarla

### Requirement: Captura de datos del docente, designación y justificación

El sistema SHALL capturar los datos del docente (DNI obligatorio, Nombre y apellido obligatorio, Legajo, Email institucional, Teléfono), la designación solicitada (Materia obligatoria, Comisión, Cargo obligatorio, Horas obligatorio, Dedicación, Antigüedad) y una justificación obligatoria con un mínimo de 20 caracteres y un máximo de 1000, mostrando un contador de caracteres.

Cuando el tipo sea "Cambio de cargo", el sistema SHALL mostrar el bloque comparativo "Actual → Solicitado".

#### Scenario: Contador de caracteres en la justificación

- **WHEN** el usuario escribe en el campo de justificación
- **THEN** el contador refleja la cantidad de caracteres ingresados sobre el máximo (formato `N / 1000`)

#### Scenario: Bloque comparativo solo en Cambio de cargo

- **WHEN** el tipo de pedido seleccionado es "Cambio de cargo"
- **THEN** la sección Designación muestra el bloque "Actual → Solicitado"; para los demás tipos ese bloque no aparece

### Requirement: Límite de tamaño de los archivos de Documentación

El sistema SHALL rechazar cualquier archivo cuyo tamaño supere los 5 MB en todos los slots de la sección Documentación (CV en PDF, DNI frente, DNI dorso y otros documentos). El archivo rechazado NO SHALL incorporarse al pedido, y el slot correspondiente SHALL mostrar un mensaje de error indicando que se superó el límite, sin afectar a los archivos ya cargados. El texto de ayuda de cada slot SHALL anunciar el límite máximo antes de cargar.

#### Scenario: Rechazo de un archivo que supera el límite

- **WHEN** el usuario selecciona para un slot de Documentación un archivo de más de 5 MB
- **THEN** el archivo no se incorpora al pedido y el slot muestra un mensaje de error indicando que se superó el límite de 5 MB

#### Scenario: Archivo dentro del límite

- **WHEN** el usuario selecciona un archivo de 5 MB o menos
- **THEN** el archivo se incorpora al slot correspondiente y no se muestra error de tamaño

### Requirement: Componentes provistos por la librería de UI

El sistema SHALL construir los controles del formulario con los componentes de `@ars-docendi/ui` (`Field`, `Input`, `Select`, `Textarea`, `FileUpload`, `InlineAlert`, `Button`, `Breadcrumbs`), sin reimplementar esos primitivos dentro de la feature.

#### Scenario: Controles montados desde la librería

- **WHEN** se renderiza cualquier campo, select, textarea, carga de archivos, alerta o botón del formulario
- **THEN** ese control es una instancia del componente correspondiente de `@ars-docendi/ui`
