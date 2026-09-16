import { RequirePermission } from "../../shared/auth/RequirePermission";

export const routes = {
  element: <RequirePermission permission="roles.ver" />,
  children: [
    {
      path: "/roles",
      lazy: () => import("./pages/IndexPage").then(({ IndexPage }) => ({ Component: IndexPage })),
    },
  ],
};
