import { Navigate, type RouteObject } from "react-router-dom";

import { RequirePermission } from "../../shared/auth/RequirePermission";
import { AdministracionAsistentePage } from "./pages/AdministracionAsistentePage";
import { SoporteHistorialPage } from "./pages/SoporteHistorialPage";

/**
 * La ruta índice YA NO ES UNA PÁGINA (ARS-151, tasks.md §10): el asistente vive
 * sólo en el modal del lanzador de la barra. `/asistente` redirige a la home
 * (`/portal`) con la marca `?asistente=abrir`, que `LanzadorAsistente` consume:
 * con el permiso abre el modal y borra la marca de la URL; sin él sólo la borra
 * (design.md D8 de asistente-rediseno-v3). La redirección misma no mira el
 * permiso —eso lo sigue decidiendo el backend, en el lanzador— así que un vínculo
 * viejo nunca queda en una pantalla muerta.
 *
 * `soporte-historial` SÍ usa `RequirePermission` (asistente-acceso-de-soporte-al-historial):
 * es una lectura de OTRA persona, así que además del gate del backend en cada
 * endpoint (§9), la ruta misma no resuelve sin el permiso — no hay elemento que
 * montar, y por eso tasks.md 13.1 la pide «sin match de ruta» y no sólo sin link.
 *
 * `administracion` sigue el MISMO criterio (asistente-administracion-de-uso,
 * tasks.md 11.1): editar presupuestos y el kill switch es una acción
 * administrativa, no una lectura propia, así que sin `asistente.administrar`
 * tampoco hay ruta que resuelva.
 */
export const routes: RouteObject = {
  path: "asistente",
  children: [
    { index: true, element: <Navigate to="/portal?asistente=abrir" replace /> },
    {
      element: <RequirePermission permission="asistente.leer_historial_ajeno" />,
      children: [{ path: "soporte-historial", element: <SoporteHistorialPage /> }],
    },
    {
      element: <RequirePermission permission="asistente.administrar" />,
      children: [{ path: "administracion", element: <AdministracionAsistentePage /> }],
    },
  ],
};
