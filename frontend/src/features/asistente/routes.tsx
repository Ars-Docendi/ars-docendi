import type { RouteObject } from "react-router-dom";

import { AsistentePage } from "./pages/AsistentePage";

/**
 * La ruta NO se protege con una lista de roles.
 *
 * El acceso lo decide el backend por permiso: `/api/asistente/capacidades` responde
 * 403 a quien no lo tiene, y el panel muestra ese rechazo en español. Una lista de
 * roles acá fallaría ABIERTA con cualquier rol nuevo —`identity.roles` no es un
 * catálogo cerrado—, y el fallo no daría error: le mostraría la pantalla a alguien
 * que no debería verla.
 *
 * Quien no tiene el permiso tampoco ve el lanzador, así que no hay forma de llegar
 * acá sin escribir la URL a mano.
 */
export const routes: RouteObject = {
  path: "asistente",
  children: [{ index: true, element: <AsistentePage /> }],
};
