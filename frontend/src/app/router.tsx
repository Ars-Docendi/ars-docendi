import { createBrowserRouter, Navigate } from "react-router-dom";

import AppLayout from "./AppLayout";
import { LoginPage } from "../shared/auth/LoginPage";
import { RequireAuth } from "../shared/auth/RequireAuth";
import { routes as designacionesRoutes } from "../features/designaciones/routes";
import { routes as aulasRoutes } from "../features/aulas/routes";
import { routes as portalRoutes } from "../features/portal/routes";
import { routes as tareasRoutes } from "../features/tareas/routes";
import { routes as personasRoutes, redireccionesAnteriores } from "../features/personas/routes";
import { routes as rolesRoutes } from "../features/roles/routes";
import { routes as sistemaRoutes } from "../features/sistema/routes";
import { routes as ayudaRoutes } from "../features/ayuda/routes";

export const router = createBrowserRouter([
  // Full-bleed split-pane login — public, rendered outside the App shell (no header/nav).
  { path: "/login", element: <LoginPage /> },
  {
    // Gate — every protected route lives below here; redirects to /login when unauth.
    element: <RequireAuth />,
    children: [
      {
        path: "/",
        element: <AppLayout />,
        children: [
          { index: true, element: <Navigate to="/portal" replace /> },
          designacionesRoutes,
          aulasRoutes,
          portalRoutes,
          tareasRoutes,
          personasRoutes,
          ...redireccionesAnteriores,
          rolesRoutes,
          ...sistemaRoutes,
          ayudaRoutes,
          { path: "/membresia-roles", element: <Navigate to="/roles" replace /> },
        ],
      },
    ],
  },
]);
