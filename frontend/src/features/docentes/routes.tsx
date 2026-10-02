import { RequirePermission } from "../../shared/auth/RequirePermission";

export const routes = {
  element: <RequirePermission permission="docentes.ver" />,
  children: [
    {
      path: "/docentes",
      lazy: () => import("./pages/IndexPage").then(({ IndexPage }) => ({ Component: IndexPage })),
    },
  ],
};
