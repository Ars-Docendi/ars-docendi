## Why

La superficie del asistente (`frontend/src/features/asistente/`) funciona y cumple la spec de accesibilidad del change `asistente-frontend`, pero tiene defectos que no son cosméticos: Enter durante un turno en vuelo dispara un segundo POST concurrente —dos claves de idempotencia, dos cobros, y el segundo sale con un hilo viejo o nulo—; un hilo vencido por inactividad deja al usuario en un bucle de 404 porque el cliente no lo suelta; un error no ofrece reintento aunque la clave por intento existe exactamente para eso; cerrar el modal —incluso por un clic afuera sin querer— destruye la conversación; el `razonamiento` que el backend redacta para el usuario final no se pinta (ARS-79); un request colgado queda en vuelo para siempre porque no hay `AbortSignal` ni timeout; la tabla ancha se recorta sin aviso por el `overflow: hidden` del envoltorio de la librería; y `columnas[].sensible` llega y se ignora. Encima, el CSS usa tokens que no existen en `@ars-docendi/ui/theme.css` y se ve con los fallbacks —gris slate y un indigo ajeno al acento institucional—, la página y el modal se ven distinto, y no hay responsive.

**Por qué ahora.** El módulo entra en producción real y esta es la única pantalla con la que todos los roles lo tocan. Los usuarios comparan con Claude y ChatGPT; lo que se puede igualar sin mentir se iguala, y lo que exigiría un backend que no existe —streaming, etapas, regenerar, feedback, historial— se deja afuera y se dice por qué.

## What Changes

**Defectos (red-green):**

- El composer no envía mientras hay un turno en vuelo: ni por Enter ni por el botón. Se puede seguir escribiendo.
- Un 404 de hilo perdido limpia el hilo del cliente; la siguiente pregunta abre una conversación nueva en lugar de repetir el error.
- Cada turno viaja con `AbortSignal` y un timeout de 160 s por request (apenas sobre el presupuesto de 150 s del backend). Desmontar el dueño de la conversación aborta el turno en vuelo; el timeout se muestra como error con reintento.
- La tabla de resultados scrollea dentro de su propio marco (override de clase sobre el envoltorio de la librería, sin `!important` ni fork), con cabecera pegajosa y celdas numéricas alineadas.
- Las columnas sensibles se marcan con un candado y texto para lector de pantalla, más una leyenda bajo la tabla.
- `/asistente` sin permiso muestra sólo el aviso de sin acceso, no un formulario que no puede funcionar.

**Comportamiento nuevo, todo con backend real hoy:**

- **«Reintentar»** en un turno que terminó en error, reusando la clave de idempotencia del intento. Sólo cuando el turno terminó en error: nunca en vuelo ni tras «Dejar de esperar», porque el backend no protege un reintento con la misma clave mientras el original sigue en curso.
- **«Dejar de esperar»** mientras hay un turno en vuelo: aborta el request del cliente y libera el campo. El backend sigue procesando y cobra la cuota; el copy lo dice y no promete ahorro.
- **«Nueva conversación»**: vacía el hilo y arranca de cero (`hilo: null`, que el backend ya acepta).
- **La conversación sobrevive al cierre del modal**: el estado de `useAsistente` sube al dueño del montaje (`LanzadorAsistente` / `AsistentePage`) y `PanelAsistente` lo recibe por prop. Página y modal siguen siendo hilos independientes. Sin `localStorage`.
- **`razonamiento`** como `<details>` colapsado «Cómo lo interpreté» dentro del mensaje (cierra ARS-79, variante 1). `preguntaInterpretada` sigue visible como «Entendí: …».
- **`metricas.categoria` sale del tipo TS**: es una etiqueta interna (`consulta_simple`, `cruce_de_tablas`); el backend la sigue mandando y el cliente la ignora. `cubre[].nombre` tampoco se muestra; sí `cubre[].descripcion`.
- **Composer** `EntradaDePregunta`: `maxLength` 2 000 con contador visible desde 1 800; botón **«Enviar»** con etiqueta visible (el lanzador sigue «Preguntar»); Enter envía y Shift+Enter hace salto; con puntero grueso Enter hace salto; auto-crecimiento hasta 6 líneas con fallback JS para Firefox/Safari.
- **Estado inicial** construido sólo con el catálogo de `GET /capacidades`: título, alcance, chips con los ejemplos, «Puedo consultar:» con las descripciones, «No puedo:» con los límites.
- **Scroll anclado**: al enviar va al fondo; al llegar la respuesta muestra el inicio de la tarjeta; si el usuario subió no se lo arrastra y aparece «Ir al final».
- **Copiar respuesta / Copiar tabla** (TSV con cabecera), sólo si `navigator.clipboard` existe.
- **Foco**: al cerrar el modal vuelve al lanzador; mientras está abierto, `inert` sobre `#root` contiene el Tab.
- **Presentación**: `asistente.css` reescrito con tokens reales del tema, sin duplicados; una franja de estado que une indicador y métricas; página y modal con el mismo aspecto y alto fijo para que sólo scrollee el hilo; modal de 880 px; pantalla completa en ≤ 640 px; `prefers-reduced-motion`.
- Seis íconos SVG a mano en `app/shell/icons.tsx` (grilla 18, trazo 1.5). Sin `lucide-react`.

**Lo que no cambia:** el contrato HTTP, el backend, la región viva acotada a la lista de mensajes, el indicador de un solo estado con umbral, el degradado como estado, opciones ≠ sugerencias, el truncado sin números, el gate por permiso real.

## Capabilities

### New Capabilities

- `asistente-conversacion`: la conversación del asistente en el frontend —composer, estado inicial, mensajes con razonamiento y acciones, reintento, «Dejar de esperar», conversación nueva, conversación que sobrevive al cierre del modal, scroll anclado, tabla con scroll propio y columnas sensibles marcadas, foco del modal, aspecto con tokens del tema y responsive—.

### Modified Capabilities

- Ninguna. Las capabilities `asistente-superficie-frontend` y `asistente-accesibilidad` del change `asistente-frontend` siguen vigentes y este change las respeta, pero ese change figura Complete sin archivar y sus specs no existen todavía en `openspec/specs/`, así que un delta `## MODIFIED Requirements` no tendría base contra la cual sincronizar. Todo lo nuevo va como `## ADDED Requirements` en la capability nueva; las specs anteriores no se tocan.

## Impact

- **Módulos afectados:** sólo frontend (`frontend/src/features/asistente/` y `frontend/src/app/shell/icons.tsx`) y docs (`docs/product/designs/asistente-conversacional-design-spec.md`, `docs/architecture/domains/asistente.md`, `docs/quality/tech-debt.md`). Backend: cero líneas; se leyó `IdempotenciaEnMemoria.cs` y `AsistenteController.cs` para una decisión.
- **API pública:** sin cambios. El cliente deja de leer `metricas.categoria`; el backend la sigue emitiendo.
- **Grafo de dependencias:** sin cambios. Ninguna dependencia nueva (`lucide-react` descartada).
- **Consumidores cross-module:** ninguno; es una feature aislada del frontend.
- **Tests:** dos ajustes deliberados en `asistente.test.tsx` —el botón de envío pasa de «Preguntar» a «Enviar»; `categoria` sale de las fixtures— y tests nuevos por fase, en rojo primero para los defectos.
- **Rollback:** revertir los commits del change. No hay migraciones, ni cambios de contrato, ni estado persistido en el navegador.
- **Tickets:** cierra ARS-79 (variante 1).
