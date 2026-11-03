## ADDED Requirements

### Requirement: El texto señala los elementos que el resultado ya vinculó

El sistema SHALL acompañar la respuesta con la posición, dentro del texto final, de cada aparición del valor de una celda que tiene vínculo, junto con el tipo y el identificador de ese vínculo.

El sistema MUST NOT señalar en el texto un valor que no tenga vínculo en el resultado: un número que aparece sólo en la prosa, o cuya celda no se resolvió, se muestra como texto.

El sistema SHALL calcular las posiciones sobre el texto que efectivamente se le muestra al usuario, después de cualquier reescritura, en unidades de código UTF-16.

El cliente SHALL pintar cada posición señalada como un enlace al destino del tipo, y MUST NOT pintar enlace alguno para un tipo que no conoce.

#### Scenario: El número del trámite vinculado se enlaza en el texto

- **GIVEN** un turno cuya tabla vincula la celda «2026-9005» a un trámite
- **WHEN** el texto de la respuesta dice «El pedido 2026-9005 está en revisión»
- **THEN** la respuesta señala la posición de «2026-9005» en el texto con el tipo y el identificador del vínculo
- **AND** el cliente muestra ese número como un enlace al detalle del trámite

#### Scenario: Un número que no está vinculado se queda como texto

- **GIVEN** un turno cuyo texto menciona «2026-8000» y cuya tabla no tiene vínculo con ese valor
- **WHEN** se arma la respuesta
- **THEN** la respuesta no señala «2026-8000» en el texto

#### Scenario: Sin vínculos no hay nada que señalar

- **GIVEN** un turno sin vínculos en el resultado
- **WHEN** se arma la respuesta
- **THEN** la respuesta no señala ninguna posición en el texto

### Requirement: Una respuesta sobre un único elemento lo presenta en una ficha

Cuando el resultado tiene exactamente una fila y esa fila tiene exactamente un vínculo, el sistema SHALL designar ese elemento como el principal de la respuesta y SHALL pedir su ficha al módulo dueño.

El sistema SHALL obtener la ficha con el mismo criterio de autorización que el detalle de ese módulo. Cuando el módulo no devuelve la ficha, la respuesta MUST NOT tener elemento principal.

Los datos de la ficha MUST NOT pasar por el modelo.

Un fallo al obtener la ficha MUST NOT impedir que el turno responda.

#### Scenario: Una fila con un trámite vinculado trae su ficha

- **GIVEN** un actor que puede abrir el trámite 2026-9005
- **WHEN** el asistente le responde con una sola fila cuyo único vínculo es ese trámite
- **THEN** la respuesta trae como principal ese trámite con su ficha

#### Scenario: Varias filas no tienen principal

- **GIVEN** un turno con tres filas vinculadas
- **WHEN** se arma la respuesta
- **THEN** la respuesta no trae elemento principal

#### Scenario: Un proveedor de fichas caído no tumba el turno

- **GIVEN** un turno con una sola fila vinculada
- **WHEN** obtener la ficha falla
- **THEN** el turno responde con su texto, su tabla y sus vínculos, sin elemento principal

#### Scenario: Sin adaptador no hay ficha

- **GIVEN** el módulo del asistente registrado sin adaptador de fichas
- **WHEN** un turno devuelve una sola fila vinculada
- **THEN** responde normalmente, sin elemento principal
