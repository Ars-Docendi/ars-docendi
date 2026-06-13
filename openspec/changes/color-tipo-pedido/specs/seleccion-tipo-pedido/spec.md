## ADDED Requirements

### Requirement: Color semántico por tipo de pedido en el estado seleccionado

El selector de tipo de pedido (sección 1 del Pedido de Designación) SHALL aplicar, a la tarjeta seleccionada, un tono semántico propio según el tipo, en vez de un color de selección único. El mapeo SHALL ser:

- `alta-nueva` → tono `success`
- `renovacion` → tono `info`
- `cambio` → tono `warning`
- `baja` → tono `danger`

El tono SHALL reusar la paleta semántica existente del design system (`--success/info/warning/danger-{100,500,700}`) sin introducir colores nuevos, y SHALL aplicar la misma estructura visual que el estado seleccionado actual (fondo escala 100, borde escala 500, ícono y título escala 700).

#### Scenario: Selección de "Baja" pinta la tarjeta en rojo

- **WHEN** el usuario selecciona la tarjeta "Baja"
- **THEN** la tarjeta seleccionada SHALL mostrarse con el tono `danger` (rojo) en fondo, borde, ícono y título

#### Scenario: Cada tipo usa su tono semántico

- **WHEN** el usuario selecciona "Alta nueva", "Renovación" o "Cambio de cargo"
- **THEN** la tarjeta seleccionada SHALL usar respectivamente el tono `success` (verde), `info` (azul) o `warning` (ámbar)

#### Scenario: Tarjetas no seleccionadas mantienen el estilo neutro

- **WHEN** una tarjeta no está seleccionada y no está bajo el cursor
- **THEN** SHALL conservar el estilo neutro actual (sin tono semántico aplicado), independientemente de su tipo

#### Scenario: Hover anticipa el tono a media intensidad

- **WHEN** el usuario posa el cursor sobre una tarjeta no seleccionada
- **THEN** la tarjeta SHALL mostrar un anticipo de su tono (borde en la escala 500 y un fondo a media intensidad, más claro que el fondo del estado seleccionado)
- **AND** el hover sobre una tarjeta ya seleccionada SHALL conservar el estado seleccionado sin atenuarlo

### Requirement: Catálogo de tipos define el tono como dato

El catálogo de tipos de pedido (`TIPOS_PEDIDO`) SHALL declarar el tono de cada tipo como dato, de modo que el render del selector permanezca data-driven y el tono viaje junto al tipo cuando el catálogo provenga del backend.

#### Scenario: El render deriva el tono del catálogo

- **WHEN** el selector renderiza las tarjetas a partir de `TIPOS_PEDIDO`
- **THEN** cada tarjeta SHALL derivar su tono del dato declarado en el catálogo, sin mapeos hardcodeados en el componente

### Requirement: Preservación de la accesibilidad del radiogroup

El cambio de color SHALL preservar la semántica accesible del selector. El contenedor SHALL seguir siendo un `radiogroup` y cada tarjeta un `radio` con `aria-checked` reflejando la selección. El color SHALL ser un refuerzo visual, nunca el único indicador de selección.

#### Scenario: La selección se comunica más allá del color

- **WHEN** una tarjeta está seleccionada
- **THEN** SHALL exponer `aria-checked="true"` y conservar un indicador no cromático de selección (borde/estado), de modo que la selección sea percibible sin depender del color

#### Scenario: Navegación por teclado intacta

- **WHEN** el usuario navega el selector con teclado
- **THEN** el foco visible y la operación del radiogroup SHALL comportarse igual que antes del cambio de color
