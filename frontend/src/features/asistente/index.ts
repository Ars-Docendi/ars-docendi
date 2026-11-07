/**
 * La API pública de la feature. Exporta dos cosas donde las otras ocho features exportan sólo
 * `routes`, porque `routes` acá no alcanza: `soporte-historial` y `administracion` siguen siendo
 * rutas propias, y el asistente mismo se abre desde `LanzadorAsistente`, montado en la barra y no
 * en ninguna ruta (desde ARS-151, tasks.md §10, `/asistente` es sólo el redirect que lo abre).
 */
export { routes } from "./routes";
export { LanzadorAsistente } from "./components/LanzadorAsistente";
// `features/sistema` compone esto en su pestaña Asistente (design D8, sistema-seccion-unificada);
// pasa por acá por la misma razón que `LanzadorAsistente` — sin este export, `app/router.tsx`
// tendría que alcanzar `components/…` directo, el import profundo cross-frontera que
// `no-restricted-imports` prohíbe.
export { PanelAdministracionAsistente } from "./components/PanelAdministracionAsistente";
// La pestaña «Modelo» de Sistema (asistente-modelo-en-caliente, tarea 4): se
// compone desde `app/router.tsx` igual que el panel de uso, por la misma razón.
export { PanelModeloDelAsistente } from "./components/PanelModeloDelAsistente";
// La pestaña «Calidad» de Sistema (asistente-metricas-de-calidad, tarea 4): se compone
// desde `app/router.tsx` igual que las otras dos, junto con el hook que pone el punto
// de estado de la pestaña en advertencia cuando la tasa de negativas sube.
export { PanelDeCalidad } from "./components/PanelDeCalidad";
export { useCalidadEnAlza } from "./hooks/useCalidadEnAlza";
