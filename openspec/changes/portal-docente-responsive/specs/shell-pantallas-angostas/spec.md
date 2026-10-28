# Spec Delta

## Purpose

Define cómo se presenta el shell autenticado (navegación y área de contenido) cuando el viewport es angosto, para que las pantallas se puedan usar desde un teléfono.

## ADDED Requirements

### Requirement: Contenido a ancho completo en pantallas angostas

Con un viewport de menos de 768 px de ancho, el shell MUST NOT reservar una columna para la barra lateral. El área de contenido SHALL ocupar todo el ancho disponible debajo de la barra superior. Con 768 px o más, el shell SHALL conservar la barra lateral fija.

#### Scenario: Teléfono

- **GIVEN** un usuario autenticado con un viewport de 375 px de ancho
- **WHEN** se renderiza cualquier pantalla del shell
- **THEN** la barra lateral fija no está visible
- **AND** el área de contenido empieza en el borde izquierdo de la pantalla

#### Scenario: Escritorio

- **GIVEN** un usuario autenticado con un viewport de 1280 px de ancho
- **WHEN** se renderiza cualquier pantalla del shell
- **THEN** la barra lateral fija se muestra a la izquierda del contenido, como hasta ahora

### Requirement: Navegación en panel lateral

Con un viewport de menos de 768 px, la navegación principal SHALL estar disponible en un panel lateral que se abre desde la barra superior. El panel MUST mostrar los mismos ítems que la barra lateral, filtrados por los permisos del usuario, y al final el acceso al Centro de ayuda. El panel SHALL cerrarse al elegir un destino, con Escape o al tocar fuera de él.

#### Scenario: Abrir el menú y navegar

- **GIVEN** un usuario autenticado en un viewport angosto
- **WHEN** toca "Menú" en la barra superior y elige un ítem de navegación
- **THEN** el sistema navega a la ruta del ítem
- **AND** el panel queda cerrado

#### Scenario: Cerrar sin navegar

- **GIVEN** el panel de navegación abierto
- **WHEN** el usuario presiona Escape o toca fuera del panel
- **THEN** el panel se cierra y la pantalla actual no cambia

#### Scenario: Ítems filtrados por permiso

- **GIVEN** un usuario sin permisos de administración
- **WHEN** abre el panel de navegación
- **THEN** el panel no muestra los ítems de administración, igual que la barra lateral de escritorio

#### Scenario: Centro de ayuda desde el teléfono

- **GIVEN** un usuario autenticado en un viewport angosto
- **WHEN** abre el panel de navegación
- **THEN** el panel ofrece "Centro de ayuda", que lleva a `/ayuda` y cierra el panel
