import { RequirePermission } from "../../shared/auth/RequirePermission";

export const routes = {
  element: <RequirePermission permission="usuarios.ver" />,
  children: [
    {
      path: "/usuarios",
      lazy: () => import("./pages/IndexPage").then(({ IndexPage }) => ({ Component: IndexPage })),
    },
  ],
};
