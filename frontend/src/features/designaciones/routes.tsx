import type { RouteObject } from "react-router-dom";
import { RequireRole } from "../../shared/auth/RequireRole";
import { IndexPage } from "./pages/IndexPage";
import { PeriodosPage } from "./pages/PeriodosPage";
import { MisPedidosPage } from "./pages/MisPedidosPage";
import { PedidoFormPage } from "./pages/PedidoFormPage";
import { TableroRevisionPage } from "./pages/TableroRevisionPage";
import { DetallePedidoPage } from "./pages/DetallePedidoPage";

export const routes: RouteObject = {
  path: "designaciones",
  children: [
    { index: true, element: <IndexPage /> },
    {
      // Gestión de períodos es exclusiva de Secretaría Académica.
      element: <RequireRole allowedRoles={["Secretaría"]} />,
      children: [{ path: "periodos", element: <PeriodosPage /> }],
    },
    {
      // SCRUM-7: la carga de pedidos es del Jefe de Cátedra.
      element: <RequireRole allowedRoles={["Jefe de Cátedra"]} />,
      children: [
        { path: "mis-pedidos", element: <MisPedidosPage /> },
        { path: "pedidos/nuevo", element: <PedidoFormPage /> },
        { path: "pedidos/:id/editar", element: <PedidoFormPage /> },
      ],
    },
    {
      // SCRUM-8: el tablero de revisión es de los revisores (Coord/Secretaría/Decanato/Administración).
      element: (
        <RequireRole allowedRoles={["Coordinador", "Secretaría", "Decanato", "Administración"]} />
      ),
      children: [{ path: "revision", element: <TableroRevisionPage /> }],
    },
    // SCRUM-8: el detalle es accesible a cualquier rol; la visibilidad se acota por ámbito en la página.
    { path: "pedidos/:id", element: <DetallePedidoPage /> },
  ],
};
