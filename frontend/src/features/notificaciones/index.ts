/**
 * La API pública de la feature: la campana y el panel que el shell compone, el hook del
 * contador y lo que hace falta para navegar a la entidad de un aviso.
 */
export { CampanaSidebar, CampanaTopBar } from "./components/CampanaNotificaciones";
export { PanelNotificaciones } from "./components/PanelNotificaciones";
export { useCantidadNoLeidas } from "./hooks/useNotificaciones";
export { rutaDeEntidad } from "./presentacion";
// Web Push: el shell sincroniza la suscripción, abre el aviso tocado y la da de baja al salir.
export { desuscribirDelServidor } from "./push/dispositivoPush";
export { useAbrirDesdePush } from "./push/useAbrirDesdePush";
export { useSincronizarPush } from "./push/useSincronizarPush";
export type { Notificacion } from "./types";
// Constructores de prueba, para los tests del shell que montan el layout real.
export { notificacion, pagina } from "./pruebas";
