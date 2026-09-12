## Why

El asistente contesta «el 2026-9005 está devuelto» y ahí termina. Lo que sigue —abrir ese trámite— el usuario lo hace a mano: se acuerda del número, cierra el modal, va a Designaciones, busca. La respuesta identifica exactamente una fila de `designaciones.pedidos` y el sistema ya tiene una pantalla que la muestra; no ofrecer el paso es dejar el trabajo a mitad de camino.

**Pero el vínculo sólo vale si el usuario puede abrir lo que se le ofrece.** Un enlace que termina en 403 es fake UI: aparenta estar hecho y no funciona (invariante #7).

Y ahí está el problema real, que no es de interfaz. **Hay dos implementaciones distintas de «quién alcanza este pedido», y ya divergen hoy:**

|                 | RLS del asistente                                                | `MaquinaEstadosPedido.AlcanzaAmbito`                                                                   |
| --------------- | ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| ámbito global   | `identity.roles.scope = 'global'`                                | lista fija de tres códigos: `secretaria`, `decanato`, `administrativo`                                 |
| roles del actor | todas las asignaciones vigentes, leídas en vivo                  | sólo roles de **sistema**, y filtrados por el rol **seleccionado** en la sesión (`ICurrentUser.Roles`) |
| permiso         | `identity.asistente_tiene_permiso('designaciones.ver')`, en vivo | claim `permiso` del token, emitido al iniciar sesión                                                   |

Las tres filas producen casos donde el asistente devuelve la fila y `GET /api/designaciones/pedidos/{id}` responde 403:

- un rol creado por Secretaría con `scope = 'global'` —la matriz es editable sin desplegar— es global para la RLS y no lo es para `EsDeptoWide`;
- un usuario con dos roles que tiene uno solo seleccionado ve, por el asistente, el ámbito de los dos;
- un permiso concedido a mitad de sesión ya rige en la RLS y todavía no está en el token.

Construir el botón sobre la RLS del asistente sería construirlo sobre la implementación equivocada: la que decide si el link funciona es la otra.

## What Changes

- **La respuesta del turno puede traer vínculos.** Cada uno señala una celda de la tabla —fila y columna— y dice a qué tipo de cosa apunta y con qué identificador. Un turno sin filas, o cuyas filas no identifican nada abrible, no trae ninguno.
- **La autoridad es el módulo dueño, no el asistente.** Designaciones expone en su `Contracts` una consulta que, dados números de trámite, devuelve **sólo** los que el actor autenticado puede abrir, con el mismo criterio que su endpoint de detalle.
- **El asistente declara un puerto y no referencia a Designaciones.** La composición vive en el Host, que es el único proyecto que ve a los dos módulos. No se agrega ninguna arista `Modules.Asistente → Modules.<X>.Contracts`: ésa es ARS-46 y necesita la aprobación del equipo.
- **El backend dice QUÉ, el frontend dice DÓNDE.** El vínculo viaja como tipo + identificador; la ruta la resuelve el cliente, que es donde viven las rutas. Un tipo que el cliente no conoce no se pinta.
- **La celda del número se vuelve el enlace.** No hay columna nueva: en el modal el ancho ya está comprometido, y el identificador es lo que el usuario iba a copiar de todos modos.
- **Navegar cierra el modal.** La conversación vive en el lanzador, que sigue montado en la barra: al volver a abrirlo el hilo está donde estaba.

## Capabilities

### Added Capabilities

- `asistente-vinculos-del-resultado`: el turno puede ofrecer, para las filas que identifican un recurso del sistema, un vínculo a la pantalla que lo muestra, verificado contra la autoridad de ese recurso.

### Modified Capabilities

- `aprobacion-pedidos-designacion`: el módulo expone en su contract público la ubicación de un trámite por su número legible, acotada a lo que el actor puede abrir.

## Impact

- `Modules.Designaciones.Contracts`: `IDesignacionesQueries` deja de ser un placeholder.
- `Modules.Designaciones`: repositorio (ubicar por número), `MaquinaEstadosPedido` (sobrecarga de `AlcanzaAmbito` por materia, sin duplicar la regla), servicio nuevo.
- `Modules.Asistente`: puerto `IResolutorDeVinculos`, buscador puro, `ResultadoDelTurno`, DTO de la API.
- `ArsDocendi.Host`: el adaptador que compone los dos.
- `frontend/src/features/asistente`: tipos, mapa de destinos, `TablaDeResultado`, `LanzadorAsistente`.
- `docs/architecture/api-contracts.md`: campo nuevo en la respuesta del turno (invariante #6).
- **Sin arista nueva en `backend/manifiesto-de-aristas.json`.** Es una consecuencia del diseño y se verifica con el test del manifiesto, que ya corre.
