## ADDED Requirements

### Requirement: El cliente dibuja la tarjeta de docente y los vínculos de personas y solicitudes

El cliente SHALL resolver `persona` a `/personas/:id` y `solicitud-reserva-aula` a `/aulas/solicitudes/:id`, y SHALL dibujar la «Tarjeta de docente» con apellido y nombre, su correo institucional como enlace `mailto:` (si viene), sus designaciones (materia · cargo · carrera) y el enlace «Ver perfil». Cuando la respuesta lista varios docentes enlazados, el cliente SHALL mostrar «Tocá un nombre para ver su perfil». El cliente MUST NOT dibujar enlace ni botón sin vínculo en la respuesta.

#### Scenario: Una tarjeta con perfil

- **GIVEN** un principal de tipo `persona`
- **WHEN** se muestra la respuesta
- **THEN** aparece la tarjeta con el enlace «Ver perfil» a `/personas/<id>`

#### Scenario: Sin vínculo, sin enlace

- **GIVEN** una lista de docentes sin vínculos
- **WHEN** se muestra la respuesta
- **THEN** los nombres son texto y no aparece la sugerencia de tocar un nombre
