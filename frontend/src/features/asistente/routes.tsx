import type { RouteObject } from "react-router-dom";

import { RequirePermission } from "../../shared/auth/RequirePermission";
import { AsistentePage } from "./pages/AsistentePage";
import { SoporteHistorialPage } from "./pages/SoporteHistorialPage";

/**
 * La ruta índice NO se protege con una lista de roles.
 *
 * El acceso lo decide el backend por permiso: `/api/asistente/capacidades` responde
 * 403 a quien no lo tiene, y el panel muestra ese rechazo en español. Una lista de
 * roles acá fallaría ABIERTA con cualquier rol nuevo —`identity.roles` no es un
 * catálogo cerrado—, y el fallo no daría error: le mostraría la pantalla a alguien
 * que no debería verla.
 *
 * Quien no tiene el permiso tampoco ve el lanzador, así que no hay forma de llegar
 * acá sin escribir la URL a mano.
 *
 * `soporte-historial` SÍ usa `RequirePermission` (asistente-acceso-de-soporte-al-historial):
 * es una lectura de OTRA persona, así que además del gate del backend en cada
 * endpoint (§9), la ruta misma no resuelve sin el permiso — no hay elemento que
 * montar, y por eso tasks.md 13.1 la pide «sin match de ruta» y no sólo sin link.
 */
export const routes: RouteObject = {
  path: "asistente",
  children: [
    { index: true, element: <AsistentePage /> },
    {
      element: <RequirePermission permission="asistente.leer_historial_ajeno" />,
      children: [{ path: "soporte-historial", element: <SoporteHistorialPage /> }],
    },
  ],
};
