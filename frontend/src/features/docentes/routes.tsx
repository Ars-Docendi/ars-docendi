import { RequireRole } from "../../shared/auth/RequireRole";

export const routes = {
  element: <RequireRole allowedRoles={["Secretaría", "Administración", "Jefe de Cátedra"]} />,
  children: [
    {
      path: "/docentes",
      lazy: () => import("./pages/IndexPage").then(({ IndexPage }) => ({ Component: IndexPage })),
    },
  ],
};
