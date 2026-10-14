import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PedidoFormPage } from "./PedidoFormPage";

const estado = vi.hoisted(() => ({ errorEnviar: null as unknown }));

vi.mock("../hooks/usePedidos", () => ({
  useMisPedidos: () => ({ data: [] }),
  usePedido: () => ({ data: undefined, isLoading: false, isError: false }),
}));

vi.mock("../hooks/useCatalogosDesignaciones", () => ({
  useCatalogosDesignaciones: () => ({
    data: {
      periodoActivo: { id: "periodo-1", nombre: "Período de prueba" },
      periodos: [],
      materias: [],
      cargos: [],
      dedicaciones: [],
      tiposBaja: [],
    },
  }),
}));

vi.mock("../hooks/useAccionesPedido", () => ({
  useCrearPedido: () => ({ mutate: vi.fn(), isPending: false, isError: false, error: null }),
  useEditarPedido: () => ({ mutate: vi.fn(), isPending: false, isError: false, error: null }),
  useEnviarPedido: () => ({
    mutate: vi.fn(),
    isPending: false,
    isError: estado.errorEnviar !== null,
    error: estado.errorEnviar,
  }),
  useReenviarPedido: () => ({ mutate: vi.fn(), isPending: false, isError: false, error: null }),
}));

vi.mock("../api/catalogos", () => ({ docentesDesdeCatalogo: () => [] }));

vi.mock("../components/PedidoForm", () => ({
  PedidoForm: () => <div aria-label="Formulario de pedido" />,
}));

describe("PedidoFormPage — errores de validación del servidor", () => {
  beforeEach(() => {
    estado.errorEnviar = null;
  });

  it("presenta errores HTTP agrupados por campo en vez de ocultarlos en un mensaje genérico", () => {
    estado.errorEnviar = {
      response: {
        data: {
          errors: {
            cargoSolicitadoId: ["Seleccioná un cargo activo."],
            dedicacionSolicitadaId: ["Seleccioná una dedicación activa."],
            horas: ["Las horas deben ser mayores a cero."],
          },
        },
      },
    };

    render(
      <MemoryRouter>
        <PedidoFormPage />
      </MemoryRouter>,
    );

    expect(screen.getByLabelText("Formulario de pedido")).toBeInTheDocument();
    const alerta = screen.getByRole("alert");
    expect(alerta).toHaveTextContent("Cargo solicitado");
    expect(alerta).toHaveTextContent("Seleccioná un cargo activo.");
    expect(alerta).toHaveTextContent("Dedicación solicitada");
    expect(alerta).toHaveTextContent("Seleccioná una dedicación activa.");
    expect(alerta).toHaveTextContent("Horas");
    expect(alerta).toHaveTextContent("Las horas deben ser mayores a cero.");
    expect(alerta).not.toHaveTextContent("Ocurrió un error al guardar.");
  });
});
