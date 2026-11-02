## Contexto

`aulas.solicitudes_reserva.aula_asignada` es texto libre. No hay reservas aprobadas en ningún ambiente (confirmado por el usuario), así que la columna se reemplaza sin migrar datos.

El vocabulario vive en `portal.habilidades` (`termino`, `termino_norm` único, `sugerido`, `canonica_id`, `usos`), pero el frontend ofrece una lista fija y todo término nuevo entra con `sugerido = true`.

## Objetivos / No objetivos

**Objetivos**: catálogo de aulas, asignación sin superposición, curaduría del vocabulario, designaciones vigentes en Mi Portal y reglas de negocio de Aulas y Portal.

**No objetivos**: catálogo de comisiones (Guaraní), períodos de exámenes, disponibilidad horaria visual de las aulas, edición del perfil ajeno (ya existe en Personas).

## Decisiones

### 1. `aula_id` reemplaza `aula_asignada`

Se agrega `aula_id` con FK a `aulas.aulas` y se elimina `aula_asignada`. El CHECK de consistencia pasa a `(estado = 'aprobada') = (aula_id IS NOT NULL)`. Si apareciera una fila aprobada, la migración falla en lugar de inventar un aula. La API conserva `aulaAsignada` como nombre del aula y agrega `aulaId`, así las tablas y los avisos no cambian de forma.

### 2. Superposición garantizada por la base

`EXCLUDE USING gist (aula_id WITH =, tsrange(dia + horario_desde, dia + horario_hasta, '[)') WITH &&) WHERE (estado = 'aprobada')`, igual que `designaciones_sin_solapamiento`. El servicio verifica antes para responder un 409 `aula-ocupada` con mensaje claro, y traduce la violación `23P01` al mismo error cuando dos asignaciones compiten. Los intervalos son semiabiertos: una reserva que termina 10:00 no choca con otra que empieza 10:00.

### 3. Capacidad advierte, no bloquea

La cantidad de alumnos es aproximada (decisión del usuario). El frontend advierte; el backend no valida capacidad.

### 4. Permisos

- El catálogo se administra con `aulas.gestionar`, ya asignado a administrativo, secretaria y sys_admin. Su descripción se actualiza.
- La lista de aulas activas también la lee quien tiene `aulas.aprobar`, porque la necesita para asignar.
- La curaduría usa el permiso nuevo `portal.vocabulario.administrar` (secretaria y sys_admin). `portal.administrar` no sirve: lo tienen también jefe_catedra y decanato, que editan perfiles pero no parametrizan.

### 5. Fusión de términos

Fusionar A en B mueve los vínculos de A a B (sin duplicar), marca `A.canonica_id = B` y sube las versiones de habilidades e intereses de los perfiles afectados para que un editor abierto reciba 409. Guardar A después resuelve a B. No se fusiona un término consigo mismo, contra uno ya fusionado ni uno que ya es canónico de otros. Un término fusionado no se ofrece en el selector ni se renombra.

### 6. Vocabulario base

La migración de Portal inserta los 17 términos que hoy fija el frontend con `sugerido = false`. Es un catálogo versionado, no un fixture.

### 7. Designaciones vigentes en Mi Portal sin arista nueva

`Modules.Designaciones` ya depende de `Modules.Portal.Contracts`. En lugar de agregar la dirección inversa, el endpoint vive en Designaciones (`GET /api/designaciones/vigentes/mias`) y Mi Portal lo consume desde el frontend.

## Riesgos / Trade-offs

- Sin vista de disponibilidad, el administrativo descubre el choque al asignar (409 con el aula ocupada). Aceptable para el volumen actual.
- La fusión no se deshace automáticamente; queda en `audit.change_log`.

## Plan de migración

Migraciones incrementales en `aulas`, `portal` e `identity`, generadas con `scaffold.mjs`. Sin datos que convertir.
