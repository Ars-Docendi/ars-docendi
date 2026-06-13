import { RequireRole } from "../../shared/auth/RequireRole";
import { IndexPage } from "./pages/IndexPage";

export const routes = {
  element: <RequireRole allowedRoles={["Secretaría", "Administración"]} />,
  children: [{ path: "/usuarios", element: <IndexPage /> }],
};
