import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, Outlet, RouterProvider } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";

import { GestorDeNavegacion } from "../../../shared/navegacion/GestorDeNavegacion";
import { reiniciarGuardias } from "../../../shared/navegacion/guardias";
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
vi.mock("../api/catalogos", () => ({ docentesDesdeCatalogo: () => [] }));
// El formulario real se prueba aparte: acá interesa cómo la página bloquea la salida.
vi.mock("../components/PedidoForm", () => ({
  PedidoForm: ({
    onSucioChange,
    onCancelar,
  }: {
    onSucioChange: (sucio: boolean) => void;
    onCancelar: () => void;
  }) => (
    <>
      <button type="button" onClick={() => onSucioChange(true)}>
        Cargar un dato
      </button>
      <button type="button" onClick={onCancelar}>
        Cancelar
      </button>
    </>
  ),
}));

afterEach(reiniciarGuardias);

function montar() {
  const router = createMemoryRouter(
    [
      {
        element: (
          <>
            <Outlet />
            <GestorDeNavegacion />
          </>
        ),
        children: [
          { path: "/designaciones/mis-pedidos", element: <p>Mis pedidos</p> },
          { path: "/designaciones/pedidos/nuevo", element: <PedidoFormPage /> },
        ],
      },
    ],
    {
      initialEntries: ["/designaciones/mis-pedidos", "/designaciones/pedidos/nuevo"],
      initialIndex: 1,
    },
  );
  render(<RouterProvider router={router} />);
  return router;
}

describe("PedidoFormPage — cambios sin guardar", () => {
  it("cancelar con datos cargados pide confirmar y 'Descartar cambios' sale", async () => {
    const router = montar();
    await userEvent.click(screen.getByRole("button", { name: "Cargar un dato" }));
    await userEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(await screen.findByText("Tenés cambios sin guardar.")).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/designaciones/pedidos/nuevo");

    await userEvent.click(screen.getByRole("button", { name: "Descartar cambios" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/designaciones/mis-pedidos"));
  });

  it("cancelar sin tocar nada sale sin preguntar", async () => {
    const router = montar();
    await userEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/designaciones/mis-pedidos"));
    expect(screen.queryByText("Tenés cambios sin guardar.")).not.toBeInTheDocument();
  });
});
