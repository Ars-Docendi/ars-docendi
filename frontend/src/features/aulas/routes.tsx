import type { RouteObject } from "react-router-dom";
import { RequirePermission } from "../../shared/auth/RequirePermission";

export const routes: RouteObject = {
  path: "aulas",
  children: [
    {
      // Disponible para Docente (aulas.solicitar) o Administrativo (aulas.aprobar).
      element: <RequirePermission permission={["aulas.solicitar", "aulas.aprobar"]} />,
      children: [
        {
          index: true,
          lazy: () =>
            import("./pages/IndexPage").then(({ IndexPage }) => ({ Component: IndexPage })),
        },
      ],
    },
  ],
};
