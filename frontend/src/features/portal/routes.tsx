import type { RouteObject } from "react-router-dom";
import { RequirePermission } from "../../shared/auth/RequirePermission";

export const routes: RouteObject = {
  path: "portal",
  children: [
    {
      index: true,
      lazy: () => import("./pages/IndexPage").then(({ IndexPage }) => ({ Component: IndexPage })),
    },
    {
      path: "vocabulario",
      element: <RequirePermission permission="portal.vocabulario.administrar" />,
      children: [
        {
          index: true,
          lazy: () =>
            import("./pages/VocabularioPage").then(({ VocabularioPage }) => ({
              Component: VocabularioPage,
            })),
        },
      ],
    },
  ],
};
