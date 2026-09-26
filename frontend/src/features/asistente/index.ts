/**
 * La API pública de la feature.
 *
 * Exporta dos cosas donde las otras ocho features exportan sólo `routes`, porque
 * `routes` acá no alcanza: `soporte-historial` y `administracion` siguen siendo
 * rutas propias, y el asistente mismo se abre desde `LanzadorAsistente`, montado
 * en la barra y no en ninguna ruta (desde ARS-151, tasks.md §10, `/asistente` es
 * sólo el redirect que lo abre). Lo que la regla de `no-restricted-imports`
 * impide es lo que pasaba antes de que `LanzadorAsistente` se exportara acá: que
 * `app/shell/TopBar.tsx` alcanzara `features/asistente/components/…`, el único
 * import profundo cross-frontera de todo el frontend.
 *
 * El costo de ese import no era estético. Cualquier reorganización de
 * `components/` rompía el shell, y el shell no tenía por qué saber que el
 * lanzador es un componente y no, por ejemplo, un hook con un portal.
 */
export { routes } from "./routes";
export { LanzadorAsistente } from "./components/LanzadorAsistente";
