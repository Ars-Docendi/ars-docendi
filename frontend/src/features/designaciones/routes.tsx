import type { RouteObject } from "react-router-dom";
import { RequireRole } from "../../shared/auth/RequireRole";

export const routes: RouteObject = {
  path: "designaciones",
  children: [
    {
      index: true,
      lazy: () => import("./pages/IndexPage").then(({ IndexPage }) => ({ Component: IndexPage })),
    },
    {
      // Gestión de períodos es exclusiva de Secretaría Académica.
      element: <RequireRole allowedRoles={["Secretaría"]} />,
      children: [
        {
          path: "periodos",
          lazy: () =>
            import("./pages/PeriodosPage").then(({ PeriodosPage }) => ({
              Component: PeriodosPage,
            })),
        },
      ],
    },
    {
      // SCRUM-7: la carga de pedidos es del Jefe de Cátedra.
      element: <RequireRole allowedRoles={["Jefe de Cátedra"]} />,
      children: [
        {
          path: "mis-pedidos",
          lazy: () =>
            import("./pages/MisPedidosPage").then(({ MisPedidosPage }) => ({
              Component: MisPedidosPage,
            })),
        },
        {
          path: "pedidos/nuevo",
          lazy: () =>
            import("./pages/PedidoFormPage").then(({ PedidoFormPage }) => ({
              Component: PedidoFormPage,
            })),
        },
      ],
    },
    {
      path: "pedidos/:id/editar",
      lazy: () =>
        import("./pages/PedidoFormPage").then(({ PedidoFormPage }) => ({
          Component: PedidoFormPage,
        })),
    },
    {
      // SCRUM-8: el tablero de revisión es de los revisores (Coord/Secretaría/Decanato/Administración).
      element: (
        <RequireRole allowedRoles={["Coordinador", "Secretaría", "Decanato", "Administración"]} />
      ),
      children: [
        {
          path: "revision",
          lazy: () =>
            import("./pages/TableroRevisionPage").then(({ TableroRevisionPage }) => ({
              Component: TableroRevisionPage,
            })),
        },
      ],
    },
    // SCRUM-8: el detalle es accesible a cualquier rol; la visibilidad se acota por ámbito en la página.
    {
      path: "pedidos/:id",
      lazy: () =>
        import("./pages/DetallePedidoPage").then(({ DetallePedidoPage }) => ({
          Component: DetallePedidoPage,
        })),
    },
  ],
};
