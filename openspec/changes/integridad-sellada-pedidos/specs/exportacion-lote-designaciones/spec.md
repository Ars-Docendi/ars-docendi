# Spec Delta

## ADDED Requirements

### Requirement: Exportación sólo con cadena íntegra y anclada

Antes de generar el lote, el sistema SHALL verificar la integridad de la cadena del período
[BR-designaciones-020]. Si no es íntegra, MUST responder 409 sin generar el archivo ni revelar
detalles de los hallazgos. Si es íntegra, SHALL registrar un ancla con la cabeza vigente, salvo que
ya exista una igual, y el libro MUST mostrar el hash de cabeza y su posición junto a la
identificación del período.

#### Scenario: Exportación con cadena íntegra

- **GIVEN** un período con cadena íntegra
- **WHEN** Decanato exporta el lote
- **THEN** el libro MUST incluir el hash de cabeza completo y su posición
- **AND** MUST quedar registrada un ancla con esa cabeza

#### Scenario: Exportación bloqueada

- **GIVEN** un período con un pedido cuyo estado diverge de su último sello
- **WHEN** se intenta exportar el lote
- **THEN** la API MUST responder 409 y MUST NOT generar el archivo

#### Scenario: Repetir la exportación sin cambios

- **GIVEN** un lote exportado y ningún evento posterior en el período
- **WHEN** se exporta de nuevo
- **THEN** el libro MUST mostrar el mismo hash de cabeza y MUST NOT registrarse un ancla duplicada
