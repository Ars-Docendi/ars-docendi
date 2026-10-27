import { Navigate, type RouteObject } from "react-router-dom";

import { RequirePermission } from "../../shared/auth/RequirePermission";
import { SoporteHistorialPage } from "./pages/SoporteHistorialPage";

/**
 * La ruta índice YA NO ES UNA PÁGINA (ARS-151, tasks.md §10): el asistente vive sólo en el modal
 * del lanzador de la barra.
 */
export const routes: RouteObject = {
  path: "asistente",
  children: [
    { index: true, element: <Navigate to="/portal?asistente=abrir" replace /> },
    {
      element: <RequirePermission permission="asistente.leer_historial_ajeno" />,
      children: [
        {
          element: <RequirePermission permission="usuarios.ver" />,
          children: [{ path: "soporte-historial", element: <SoporteHistorialPage /> }],
        },
      ],
    },
    {
      element: <RequirePermission permission="asistente.administrar" />,
      children: [
        {
          path: "administracion",
          element: <Navigate to={{ pathname: "/sistema", hash: "#asistente" }} replace />,
        },
      ],
    },
  ],
};
