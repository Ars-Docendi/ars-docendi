# Diseño — el asistente ofrece el vínculo al trámite

## Decisiones

### D1 — La autoridad del vínculo es Designaciones, y no la RLS que devolvió la fila

Lo tentador es razonar así: la fila llegó, luego la RLS la dejó pasar, luego el actor puede verla, luego el link funciona. Es falso, y no por un caso de borde teórico: **son dos implementaciones distintas de la misma regla, escritas en lenguajes distintos, y ya divergen.** El cuadro está en el `proposal`; las tres divergencias son ámbito global por lista fija vs. `scope`, roles del token filtrados por el rol seleccionado vs. asignaciones en vivo, y claim del token vs. matriz en vivo.

Un link que resulta de la primera implementación cuando quien lo va a atender es la segunda es exactamente la clase de fake UI que el invariante #7 prohíbe: aparenta estar hecho hasta que alguien lo aprieta.

Así que el vínculo se le pregunta **al módulo dueño del recurso**, por su `Contracts`, y con el mismo criterio que su endpoint de detalle: política de permiso primero, `AlcanzaAmbito` después. Si el módulo no lo devuelve, no hay botón.

**Consecuencia deliberada:** el asistente puede mostrar una fila y no ofrecer el link. Eso es correcto y es la dirección segura del error —se ve el dato, no se ofrece un camino que no existe—. La opuesta sería ofrecer el camino y que no exista.

### D2 — `AlcanzaAmbito` se **reusa**, no se reescribe

La consulta nueva no vuelve a escribir «global, o coordinador de esa carrera, o jefe de esa materia». Llama a `MaquinaEstadosPedido.AlcanzaAmbito`, que es la misma función que usa `ObtenerAutorizadoAsync`. Se le agrega una sobrecarga que toma la materia en vez del `Pedido` entero —ubicar por número no necesita cargar adjuntos, historial ni cargo— y la sobrecarga vieja **delega** en la nueva.

Una copia de la regla acá sería el mismo error que este cambio existe para no cometer, un nivel más abajo.

### D3 — El asistente declara el puerto; el Host compone

El asistente **no** referencia `Modules.Designaciones.Contracts`. Esa arista es ARS-46 y el propio código lo dice donde el enrutador de dominio quedó en modo sombra: _«los edges hacia Modules.<X>.Contracts todavía no existen, y los edges necesitan que el equipo apruebe el checklist de cinco pasos»_. Agregarla de contrabando para un botón sería saltearse la aprobación por la puerta de atrás.

En su lugar, el asistente declara `IResolutorDeVinculos` en su propia capa de aplicación —«dados estos textos, ¿cuáles son algo que este actor puede abrir?»— y el **Host**, que ya referencia a los dos módulos y ya compone así `ServicioDocentes` con `IAdministracionDesignaciones`, implementa el adaptador.

Ventajas que no son incidentales:

- **No hay arista nueva**, así que el manifiesto no cambia y el test del grafo lo confirma sin que haya que argumentar nada.
- El asistente **no sabe qué es un trámite**. El día que Portal quiera ofrecer «ver el perfil», se suma un brazo en el adaptador del Host y el asistente no se entera.
- El módulo del asistente sigue arrancando solo: registra una implementación que no resuelve nada, y el Host la reemplaza.

### D4 — Los candidatos salen de los valores de las celdas, no de la procedencia de la columna

Existe una forma más exacta: PostgreSQL reporta, por columna, el OID de la tabla y el `attnum`, y el enmascarador ya los usa para clasificar sensibilidad sin depender del alias. Con eso se sabría con certeza que una columna **es** `designaciones.pedidos.numero`.

No se usa, por una razón medida en el propio enmascarador: **ese par se pierde en cuanto la consulta deja de ser una proyección directa.** `DISTINCT`, `GROUP BY`, `UNION` y cualquier expresión reportan OID 0 —está documentado en `ClasificacionDeSensibilidad.Desconocida` y registrado como TD-009—, y el modelo escribe `DISTINCT` con frecuencia. El vínculo desaparecería justo en las consultas que listan varios trámites.

Se toman entonces los **valores** de las celdas y se los somete a la autoridad. La objeción obvia —«un texto cualquiera podría coincidir con un número de trámite»— se responde sola: el candidato lo valida Designaciones contra `pedidos.numero`, que es `UNIQUE`. Una coincidencia exacta de celda completa **es** ese trámite. Y si el actor no lo alcanza, tampoco hay link.

**El formato lo conoce Designaciones, no el asistente.** El asistente manda todo lo que parece un identificador —cadena sin espacios, corta, no vacía— y el módulo descarta lo que no tiene forma de número de trámite. Poner la expresión `AAAA-NNNN` en el asistente sería que el asistente supiera cómo numera Designaciones.

### D5 — El backend dice QUÉ; el frontend dice DÓNDE

El vínculo viaja como `{fila, columna, tipo, id}`. No lleva la URL.

Una ruta es una decisión del cliente: hoy `/designaciones/pedidos/:id`, mañana otra cosa, y el backend no tiene por qué enterarse. El cliente tiene un mapa de `tipo → ruta`, y **un tipo que no está en el mapa no se pinta**. Eso hace el contrato compatible hacia adelante en la dirección correcta: un backend nuevo que empiece a mandar `perfil-docente` no rompe un cliente viejo, sólo no lo aprovecha.

### D6 — La celda del número es el enlace; no hay columna nueva

En el modal el ancho ya está comprometido —tres columnas y la tabla scrollea— y una cuarta columna de «Ver» le roba lugar al dato. El número de trámite es además lo que el usuario ya iba a copiar, así que convertirlo en enlace pone la acción donde la mano ya estaba.

El nombre accesible no es el número solo: es «Ver el trámite 2026-9005», porque un lector de pantalla que anuncia «enlace, 2026-9005» no dice a dónde va.

### D7 — Navegar cierra el modal, y la conversación sobrevive

`LanzadorAsistente` cierra al cambiar la ruta. No hace falta pasar un callback por tres componentes: el lanzador ya observa la ubicación y es el dueño del estado de apertura.

La conversación no se pierde porque **no vive en el panel sino en el lanzador**, que está montado en la barra superior y no se desmonta al navegar. Es la misma propiedad que ya hace que cerrar con Escape no tire el hilo.

## Alternativas descartadas

**Enlazar siempre y que el detalle diga «no tenés permiso».** Convierte el 403 en una pantalla más prolija, pero el botón sigue prometiendo algo que no cumple. El invariante #7 no habla de cómo falla, habla de que no aparente.

**Pedirle al modelo que incluya el `id` en el `SELECT`.** Pinta una columna de UUID inútil en la tabla, o exige recortarla después —y recortar columnas del resultado es exactamente lo que el enmascarador hace con criterio de sensibilidad, no de estética—. Además queda a merced de que el modelo obedezca una instrucción, que es lo contrario de lo que hace el resto de este pipeline.

**Resolver el vínculo en el frontend.** El cliente no sabe la materia ni la carrera de cada trámite, así que no puede evaluar ámbito; tendría que preguntar igual, un pedido por fila.

**Agregar la arista `Modules.Asistente → Modules.Designaciones.Contracts`.** Es más directa y es la que ARS-46 va a traer. Se descarta ahora porque el checklist de cinco pasos no está aprobado, y porque el puerto en el asistente es mejor diseño de todos modos: el asistente no tiene por qué conocer los módulos que enlaza.
