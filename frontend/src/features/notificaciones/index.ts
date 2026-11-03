/**
 * La API pública de la feature: la campana y el panel que el shell compone, el hook del
 * contador y lo que hace falta para navegar a la entidad de un aviso.
 */
export { CampanaSidebar, CampanaTopBar } from "./components/CampanaNotificaciones";
export { PanelNotificaciones } from "./components/PanelNotificaciones";
export { useCantidadNoLeidas } from "./hooks/useNotificaciones";
export { rutaDeEntidad } from "./presentacion";
export type { Notificacion } from "./types";
// Constructores de prueba, para los tests del shell que montan el layout real.
export { notificacion, pagina } from "./pruebas";
