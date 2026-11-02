## ADDED Requirements

### Requirement: Entrada al Centro de ayuda desde la sidebar

La sidebar principal SHALL mostrar un ítem "Centro de ayuda" con ícono de signo de pregunta en su pie fijo, debajo de los grupos de navegación, para todo usuario autenticado. El ítem MUST enlazar a `/ayuda` y marcarse como actual en cualquier ruta bajo `/ayuda`. Con la sidebar contraída SHALL mostrarse solo el ícono, con `title` y etiqueta accesible "Centro de ayuda".

#### Scenario: Usuario sin permisos de módulos ve el ítem

- **GIVEN** un usuario autenticado cuyo rol no tiene ningún permiso de navegación
- **WHEN** se renderiza la sidebar
- **THEN** aparece el ítem "Centro de ayuda" en el pie y no hay grupos de navegación

#### Scenario: Sidebar contraída

- **GIVEN** la sidebar contraída
- **WHEN** se renderiza el pie
- **THEN** el ítem muestra solo el ícono con etiqueta accesible "Centro de ayuda"

### Requirement: Layout de ayuda con segunda sidebar de documentación

Bajo `/ayuda` la sidebar principal SHALL mostrarse contraída a íconos, aunque el shell esté expandido, y SHALL aparecer una segunda sidebar de documentación de 264 px con: un enlace de título al inicio de la ayuda, el buscador, los módulos visibles como grupos colapsables con sus artículos visibles, y bajo el artículo activo la lista de sus pasos. La contracción MUST derivarse de la ruta: al salir de `/ayuda` la sidebar principal recupera el estado que tenía el shell.

#### Scenario: Entrar a la ayuda contrae la sidebar principal

- **GIVEN** un usuario con la sidebar principal expandida
- **WHEN** navega a `/ayuda`
- **THEN** la sidebar principal se muestra solo con íconos
- **AND** aparece la sidebar de documentación

#### Scenario: Salir de la ayuda restaura la sidebar

- **GIVEN** un usuario en `/ayuda` cuyo shell está expandido
- **WHEN** navega a `/portal`
- **THEN** la sidebar principal vuelve a mostrarse expandida y la de documentación desaparece

#### Scenario: Artículo activo muestra sus pasos

- **GIVEN** un usuario en un artículo con cinco pasos
- **WHEN** se renderiza la sidebar de documentación
- **THEN** el módulo del artículo está expandido y el artículo activo lista sus cinco pasos como enlaces

### Requirement: Scroll-spy de pasos

La lista de pasos del artículo activo SHALL resaltar el paso visible en el contenido, calculado con `IntersectionObserver`. Hacer clic en un paso MUST desplazar la vista suavemente hasta él y actualizar el hash a `#paso-N`.

#### Scenario: Clic en un paso

- **GIVEN** un artículo abierto con tres pasos
- **WHEN** el usuario hace clic en el paso 3 de la sidebar de documentación
- **THEN** la vista se desplaza hasta el paso 3 y la URL termina en `#paso-3`

#### Scenario: Desplazamiento resalta el paso visible

- **GIVEN** un artículo abierto
- **WHEN** el paso 2 pasa a ser el paso visible al desplazarse
- **THEN** el ítem del paso 2 se marca como actual en la sidebar de documentación

### Requirement: Inicio de la ayuda

`/ayuda` SHALL listar los módulos visibles para la persona; cada fila MUST mostrar título, descripción e indicador de navegación y enlazar a `/ayuda/<modulo>`. El encabezado SHALL seguir la convención de `encabezado-paginas`: breadcrumb que comienza en "Inicio" y título "Centro de ayuda", sin pretitle.

#### Scenario: Lista de módulos

- **GIVEN** un usuario que puede ver tres módulos de ayuda
- **WHEN** abre `/ayuda`
- **THEN** ve exactamente tres filas con título y descripción
- **AND** el breadcrumb es "Inicio › Centro de ayuda"

### Requirement: Página de módulo

`/ayuda/<modulo>` SHALL mostrar breadcrumb "Inicio › Centro de ayuda › <Módulo>", el título y la descripción del módulo, y sus artículos visibles agrupados bajo los encabezados de grupo del módulo, en el orden de los grupos y, dentro de cada grupo, en el orden de declaración. Cada artículo MUST mostrarse como una fila con título, resumen e indicador de navegación. Un grupo sin artículos visibles MUST NOT mostrarse.

#### Scenario: Artículos agrupados

- **GIVEN** un módulo con dos grupos y artículos en ambos
- **WHEN** el usuario abre la página del módulo
- **THEN** los artículos aparecen bajo el encabezado de su grupo, con grupos en el orden declarado

### Requirement: Página de artículo

`/ayuda/<modulo>/<articulo>` SHALL mostrar breadcrumb de tres niveles ("Inicio › Centro de ayuda › <Módulo> › <Artículo>"), el título como `h1`, la introducción y los pasos numerados. Cada paso MUST tener `id="paso-N"` y SHALL mostrar su título, cuerpo, captura opcional y nota opcional de tipo información, importante o consejo. Si el artículo declara "¿Qué sucede después?", SHALL mostrarse esa sección con viñetas opcionales; si declara preguntas frecuentes, SHALL mostrarse un acordeón con `id="preguntas-frecuentes"`; si declara artículos relacionados visibles, SHALL listarlos. El encabezado MUST NOT mostrar pretitle (el módulo ya está en el breadcrumb).

#### Scenario: Artículo completo

- **GIVEN** un artículo con tres pasos, una nota "importante", preguntas frecuentes y un relacionado visible
- **WHEN** el usuario lo abre
- **THEN** ve el `h1`, los tres pasos numerados con la nota, el acordeón de preguntas y el artículo relacionado

#### Scenario: Deep link a un paso

- **GIVEN** un artículo con cinco pasos
- **WHEN** el usuario abre la URL con `#paso-4`
- **THEN** la vista se posiciona en el paso 4

#### Scenario: Deep link a preguntas frecuentes

- **GIVEN** un artículo con preguntas frecuentes
- **WHEN** el usuario abre la URL con `#preguntas-frecuentes`
- **THEN** la vista se posiciona en esa sección

#### Scenario: Artículo sin preguntas ni relacionados visibles

- **GIVEN** un artículo sin preguntas frecuentes y cuyos relacionados la persona no puede ver
- **WHEN** lo abre
- **THEN** no se muestra ninguna de las dos secciones, sin títulos vacíos

### Requirement: Slugs desconocidos redirigen al inicio de la ayuda

Un módulo o artículo inexistente, o un artículo que no corresponde al módulo de la URL, MUST redirigir a `/ayuda` sin renderizar contenido parcial.

#### Scenario: Módulo inexistente

- **WHEN** el usuario abre `/ayuda/no-existe`
- **THEN** es redirigido a `/ayuda`

#### Scenario: Artículo de otro módulo

- **WHEN** el usuario abre `/ayuda/tareas/<slug de un artículo de designaciones>`
- **THEN** es redirigido a `/ayuda`

### Requirement: Capturas a pantalla completa

Las capturas de un paso SHALL mostrarse inline y abrirse a pantalla completa con clic o con teclado. Esc y el clic en el fondo MUST cerrar el visor, y el foco MUST volver a la captura que lo abrió. Toda captura MUST tener texto alternativo no vacío.

#### Scenario: Abrir y cerrar con Esc

- **GIVEN** un paso con captura
- **WHEN** el usuario la activa y luego presiona Esc
- **THEN** el visor se abre y después se cierra

#### Scenario: Cerrar con el fondo

- **GIVEN** el visor abierto
- **WHEN** el usuario hace clic en el fondo
- **THEN** el visor se cierra

### Requirement: Sin controles ficticios

El Centro de ayuda MUST NOT mostrar botones, enlaces ni secciones sin funcionalidad ni contenido real: no hay videos, "próximamente", temas destacados ni contenedores de captura vacíos.

#### Scenario: Paso sin captura

- **GIVEN** un paso que no declara captura
- **WHEN** se renderiza
- **THEN** no hay contenedor de imagen ni marcador de posición
