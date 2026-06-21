import type { RouteObject } from "react-router-dom";
import { RequireRole } from "../../shared/auth/RequireRole";
import { IndexPage } from "./pages/IndexPage";
import { PeriodosPage } from "./pages/PeriodosPage";
import { MisPedidosPage } from "./pages/MisPedidosPage";
import { PedidoFormPage } from "./pages/PedidoFormPage";

export const routes: RouteObject = {
  path: "designaciones",
  children: [
    { index: true, element: <IndexPage /> },
    { path: "periodos", element: <PeriodosPage /> },
    {
      // SCRUM-7: la carga de pedidos es del Jefe de Cátedra.
      element: <RequireRole allowedRoles={["Jefe de Cátedra"]} />,
      children: [
        { path: "mis-pedidos", element: <MisPedidosPage /> },
        { path: "pedidos/nuevo", element: <PedidoFormPage /> },
        { path: "pedidos/:id/editar", element: <PedidoFormPage /> },
      ],
    },
  ],
};
