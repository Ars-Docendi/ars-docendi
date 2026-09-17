import type { RouteObject } from "react-router-dom";
import { RequirePermission } from "../../shared/auth/RequirePermission";

export const routes: RouteObject = {
  path: "designaciones",
  children: [
    {
      // Gestión de períodos es exclusiva de Secretaría Académica.
      element: <RequirePermission permission="periodos.administrar" />,
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
      element: <RequirePermission permission="designaciones.gestionar" />,
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
      element: <RequirePermission permission="designaciones.revisar" />,
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
