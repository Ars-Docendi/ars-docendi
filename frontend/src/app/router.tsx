import { createBrowserRouter, Navigate } from "react-router-dom";

import App from "../App";
import { LoginPage } from "../shared/auth/LoginPage";
import { RequireAuth } from "../shared/auth/RequireAuth";
import { routes as designacionesRoutes } from "../features/designaciones/routes";
import { routes as aulasRoutes } from "../features/aulas/routes";
import { routes as portalRoutes } from "../features/portal/routes";
import { routes as tareasRoutes } from "../features/tareas/routes";

export const router = createBrowserRouter([
  // Full-bleed split-pane login — public, rendered outside the App shell (no header/nav).
  { path: "/login", element: <LoginPage /> },
  {
    // Gate — every protected route lives below here; redirects to /login when unauth.
    element: <RequireAuth />,
    children: [
      {
        path: "/",
        element: <App />,
        children: [
          { index: true, element: <Navigate to="/designaciones" replace /> },
          designacionesRoutes,
          aulasRoutes,
          portalRoutes,
          tareasRoutes,
        ],
      },
    ],
  },
]);
