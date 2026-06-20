import { createBrowserRouter, Navigate } from "react-router-dom";

import AppLayout from "./AppLayout";
import { LoginPage } from "../shared/auth/LoginPage";
import { RequireAuth } from "../shared/auth/RequireAuth";
import { routes as designacionesRoutes } from "../features/designaciones/routes";
import { routes as aulasRoutes } from "../features/aulas/routes";
import { routes as portalRoutes } from "../features/portal/routes";
import { routes as tareasRoutes } from "../features/tareas/routes";
import { routes as usuariosRoutes } from "../features/usuarios/routes";
import { routes as rolesRoutes } from "../features/roles/routes";
import { routes as membresiaRolesRoutes } from "../features/membresia-roles/routes";
import { ConfiguracionProvider } from "../shared/configuracion/ConfiguracionContext";

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
          usuariosRoutes,
          {
            element: <ConfiguracionProvider />,
            children: [rolesRoutes, membresiaRolesRoutes],
          },
        ],
      },
    ],
  },
]);
