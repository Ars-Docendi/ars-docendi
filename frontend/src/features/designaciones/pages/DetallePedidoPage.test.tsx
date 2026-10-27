import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";

import { DetallePedidoPage } from "./DetallePedidoPage";
import type { PedidoDesignacion } from "../types";

const estado = vi.hoisted(() => ({
  permisos: [] as string[],
  pedido: undefined as unknown,
  errorAceptar: null as unknown,
}));

vi.mock("../../../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({
    user: {
      name: "Torres, Julián",
      initials: "TJ",
      upn: "jtorres@unlam.edu.ar",
      role: "Jefe de Cátedra",
      roleCode: "jefe_catedra",
      permissions: estado.permisos,
    },
    isLoading: false,
    error: null,
    retry: () => {},
  }),
}));

vi.mock("../hooks/usePedidos", () => ({
  usePedido: () => ({
    data: estado.pedido,
    isLoading: false,
    isError: estado.pedido === undefined,
  }),
}));

vi.mock("../hooks/useAccionesPedido", () => {
  const mutacion = () => ({ mutate: vi.fn(), isPending: false, isError: false, error: null });
  return {
    useAceptarPedido: () => ({
      mutate: vi.fn(),
      isPending: false,
      isError: estado.errorAceptar !== null,
      error: estado.errorAceptar,
    }),
    useRechazarPedido: mutacion,
    useDevolverPedido: mutacion,
    usePriorizarPedido: mutacion,
    useDespriorizarPedido: mutacion,
    useEliminarPedido: mutacion,
  };
});

const PEDIDO: PedidoDesignacion = {
  id: "p1",
  numero: "2026-9005",
  periodoId: "1",
  periodoNombre: "Segundo cuatrimestre 2026",
  catedra: "Ingeniería de Software",
  carrera: "Ingeniería en Informática",
  docente: { dni: "40444013", nombre: "Torres, Julián", antiguedad: 0 },
  horas: 8,
  cargoActual: "Jefe de Trabajos Prácticos",
  dedicacionActual: "Categoría 3",
  novedad: "Cambio de cargo o dedicación",
  horasExternas: 0,
  horasInvestigacion: 0,
  esAgenteExterno: false,
  adjuntos: [],
  estado: "devuelto",
  prioritario: false,
  historial: [],
};

function renderDetalle(origen?: string) {
  render(
    <MemoryRouter
      initialEntries={[
        { pathname: "/designaciones/pedidos/p1", state: origen ? { origen } : undefined },
      ]}
    >
      <Routes>
        <Route path="/designaciones/pedidos/:id" element={<DetallePedidoPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("DetallePedidoPage — encabezado", () => {
  beforeEach(() => {
    estado.permisos = ["designaciones.revisar"];
    estado.pedido = PEDIDO;
  });

  it("muestra por campo la validación del backend al aprobar un legado incompleto", () => {
    estado.permisos = ["designaciones.revisar"];
    estado.errorAceptar = {
      response: {
        data: {
          errors: {
            cargoSolicitadoId: ["Seleccioná un cargo activo."],
            horas: ["Las horas de materia deben ser mayores a cero."],
          },
        },
      },
    };
    renderDetalle("revision");

    const alerta = screen.getByRole("alert");
    expect(alerta).toHaveTextContent("Cargo solicitado");
    expect(alerta).toHaveTextContent("Seleccioná un cargo activo.");
    expect(alerta).toHaveTextContent("Horas");
    expect(alerta).toHaveTextContent("Las horas de materia deben ser mayores a cero.");
  });
});
