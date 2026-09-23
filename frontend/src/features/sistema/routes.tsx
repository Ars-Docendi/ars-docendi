import type { RouteObject } from "react-router-dom";
import { RequirePermission } from "../../shared/auth/RequirePermission";

export const routes: RouteObject[] = [
  {
    element: <RequirePermission permission="sistema.estado.ver" />,
    children: [
      {
        path: "/sistema",
        lazy: () =>
          import("./pages/DashboardPage").then(({ DashboardPage }) => ({
            Component: DashboardPage,
          })),
      },
    ],
  },
  {
    element: <RequirePermission permission="auditoria.ver" />,
    children: [
      {
        path: "/auditoria",
        lazy: () =>
          import("./pages/AuditoriaPage").then(({ AuditoriaPage }) => ({
            Component: AuditoriaPage,
          })),
      },
    ],
  },
];
