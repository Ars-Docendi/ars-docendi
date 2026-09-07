import { RequireRole } from "../../shared/auth/RequireRole";

export const routes = {
  element: <RequireRole allowedRoles={["Secretaría", "Administración"]} />,
  children: [
    {
      path: "/usuarios",
      lazy: () => import("./pages/IndexPage").then(({ IndexPage }) => ({ Component: IndexPage })),
    },
  ],
};
