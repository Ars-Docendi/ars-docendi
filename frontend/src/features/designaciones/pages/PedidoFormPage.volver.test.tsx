import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, Outlet, RouterProvider } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";

import { PedidoFormPage } from "./PedidoFormPage";

vi.mock("../hooks/usePedidos", () => ({
  useMisPedidos: () => ({ data: [] }),
  usePedido: () => ({ data: undefined, isLoading: false, isError: false }),
}));
vi.mock("../hooks/useCatalogosDesignaciones", () => ({
  useCatalogosDesignaciones: () => ({
    data: {
      periodoActivo: null,
      periodos: [],
      materias: [],
      cargos: [],
      dedicaciones: [],
      tiposBaja: [],
    },
  }),
}));
vi.mock("../hooks/useAccionesPedido", () => {
  const mutacion = () => ({ mutate: vi.fn(), isPending: false, isError: false, error: null });
  return {
    useCrearPedido: mutacion,
    useEditarPedido: mutacion,
    useEnviarPedido: mutacion,
    useReenviarPedido: mutacion,
  };
});
vi.mock("../../../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({ user: { permissions: ["designaciones.gestionar"] } }),
}));
vi.mock("../api/catalogos", () => ({ docentesDesdeCatalogo: () => [] }));
// El formulario real se prueba aparte: acá interesa adónde vuelve "Cancelar".
vi.mock("../components/PedidoForm", () => ({
  PedidoForm: ({ onCancelar }: { onCancelar: () => void }) => (
    <button type="button" onClick={onCancelar}>
      Cancelar
    </button>
  ),
}));

function montar(state: unknown) {
  const router = createMemoryRouter(
    [
      {
        element: <Outlet />,
        children: [
          { path: "/designaciones/mis-pedidos", element: <p>Mis pedidos</p> },
          { path: "/designaciones/revision", element: <p>Revisión</p> },
          { path: "/designaciones/pedidos/nuevo", element: <PedidoFormPage /> },
        ],
      },
    ],
    {
      initialEntries: [{ pathname: "/designaciones/pedidos/nuevo", state }],
    },
  );
  render(<RouterProvider router={router} />);
  return router;
}

describe("PedidoFormPage — Cancelar de un pedido nuevo", () => {
  it("vuelve a Mis pedidos con los filtros y el orden con los que se salió", async () => {
    const origen = "/designaciones/mis-pedidos?estado=borrador&orden=docente%3Aasc";
    const router = montar({ volverA: origen });
    await userEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/designaciones/mis-pedidos"));
    expect(router.state.location.search).toBe("?estado=borrador&orden=docente%3Aasc");
  });

  it("sin origen (enlace directo) vuelve a Mis pedidos sin filtros", async () => {
    const router = montar(undefined);
    await userEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/designaciones/mis-pedidos"));
    expect(router.state.location.search).toBe("");
  });

  it("ignora un origen que no es Mis pedidos", async () => {
    const router = montar({ volverA: "/designaciones/revision?estado=x" });
    await userEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/designaciones/mis-pedidos"));
    expect(router.state.location.search).toBe("");
  });
});
