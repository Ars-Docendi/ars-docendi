## ADDED Requirements

### Requirement: Búsqueda local sobre el contenido visible

La búsqueda del Centro de ayuda SHALL ejecutarse en el cliente sobre los artículos que la persona puede ver, sin llamadas al backend. SHALL normalizar consulta y texto pasando a minúsculas y quitando acentos, descartar stop-words del castellano, exigir que todos los términos restantes aparezcan (AND) como subcadena, y ordenar por puntaje: coincidencia en título 10, palabras clave 6, resumen 3 y cuerpo (introducción, pasos, preguntas frecuentes) 1, sumando por término. En empate MUST conservarse el orden del contenido.

#### Scenario: Sin acentos ni mayúsculas

- **GIVEN** un artículo titulado "Períodos de designación"
- **WHEN** se busca "PERIODOS"
- **THEN** el artículo aparece en los resultados

#### Scenario: AND entre términos

- **GIVEN** un artículo que contiene "pedido" y otro que contiene "pedido" y "adjunto"
- **WHEN** se busca "pedido adjunto"
- **THEN** solo aparece el segundo

#### Scenario: Stop-words ignoradas

- **WHEN** se busca "como crear un pedido"
- **THEN** el resultado equivale al de "crear pedido"

#### Scenario: El título pesa más que el cuerpo

- **GIVEN** un artículo que menciona "revisión" solo en el cuerpo y otro que la tiene en el título
- **WHEN** se busca "revisión"
- **THEN** el de título coincidente aparece primero

#### Scenario: Consulta vacía o solo stop-words

- **WHEN** la consulta, normalizada, no tiene términos
- **THEN** no hay resultados

### Requirement: Combobox de búsqueda en la sidebar de documentación

El buscador de la sidebar de documentación SHALL ser un combobox accesible (`role="combobox"`, `aria-expanded`, `aria-activedescendant`) que, al escribir, muestre en un panel hasta 5 resultados con título y módulo. Las flechas MUST mover la opción activa, Enter abrir la activa (o la página de resultados si no hay ninguna activa) y Esc cerrar el panel. Un botón de limpiar SHALL aparecer cuando hay texto. Si hay más de 5 coincidencias, el panel MUST incluir "Ver todos los resultados" hacia `/ayuda/buscar?q=<consulta>`. Sin coincidencias SHALL mostrarse "Sin resultados para «<consulta>»".

#### Scenario: Navegación con teclado

- **GIVEN** un panel con tres resultados
- **WHEN** el usuario presiona ↓ dos veces y Enter
- **THEN** se abre el segundo resultado

#### Scenario: Más de cinco coincidencias

- **GIVEN** ocho artículos que coinciden
- **WHEN** el usuario escribe la consulta
- **THEN** el panel muestra cinco y el enlace "Ver todos los resultados"

#### Scenario: Esc y limpiar

- **GIVEN** un panel abierto con texto
- **WHEN** el usuario presiona Esc
- **THEN** el panel se cierra
- **AND** al activar el botón de limpiar el campo queda vacío

### Requirement: Página de resultados

`/ayuda/buscar?q=<consulta>` SHALL listar todos los artículos coincidentes con título, módulo y resumen, e indicar la cantidad. Sin consulta o sin coincidencias SHALL mostrar un estado vacío con enlace al inicio de la ayuda. `buscar` MUST ser un slug reservado: ningún módulo puede usarlo.

#### Scenario: Resultados completos

- **WHEN** el usuario abre `/ayuda/buscar?q=periodo`
- **THEN** ve todos los artículos visibles que coinciden, con su módulo y resumen

#### Scenario: Sin coincidencias

- **WHEN** abre `/ayuda/buscar?q=zzzz`
- **THEN** ve el mensaje de sin resultados y un enlace al inicio de la ayuda
