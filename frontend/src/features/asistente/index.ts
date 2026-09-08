/**
 * La API pública de la feature.
 *
 * El asistente tiene DOS montajes —la ruta propia y el lanzador de la barra— y
 * por eso exporta dos cosas donde las otras ocho features exportan sólo `routes`.
 * Lo que la regla de `no-restricted-imports` impide es lo que pasaba antes: que
 * `app/shell/TopBar.tsx` alcanzara `features/asistente/components/…`, el único
 * import profundo cross-frontera de todo el frontend.
 *
 * El costo de ese import no era estético. Cualquier reorganización de
 * `components/` rompía el shell, y el shell no tenía por qué saber que el
 * lanzador es un componente y no, por ejemplo, un hook con un portal.
 */
export { routes } from "./routes";
export { LanzadorAsistente } from "./components/LanzadorAsistente";
