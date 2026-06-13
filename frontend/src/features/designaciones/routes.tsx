import type { RouteObject } from "react-router-dom";
import { IndexPage } from "./pages/IndexPage";
import { PedidoFormPage } from "./pages/PedidoFormPage";

export const routes: RouteObject = {
  path: "designaciones",
  children: [
    { index: true, element: <IndexPage /> },
    { path: "pedidos/nuevo", element: <PedidoFormPage /> },
    { path: "pedidos/:id/editar", element: <PedidoFormPage /> },
  ],
};
