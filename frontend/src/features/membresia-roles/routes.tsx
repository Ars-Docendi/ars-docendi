import { RequireRole } from "../../shared/auth/RequireRole";

export const routes = {
  element: <RequireRole allowedRoles={["Secretaría", "Administración"]} />,
  children: [
    {
      path: "/membresia-roles",
      lazy: () => import("./pages/IndexPage").then(({ IndexPage }) => ({ Component: IndexPage })),
    },
  ],
};
