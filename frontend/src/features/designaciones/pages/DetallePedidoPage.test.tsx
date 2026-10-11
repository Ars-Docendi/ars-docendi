import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";

import { DetallePedidoPage } from "./DetallePedidoPage";
import type { PedidoDesignacion } from "../types";

const estado = vi.hoisted(() => ({
  permisos: [] as string[],
  pedido: undefined as unknown,
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
    useAceptarPedido: mutacion,
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

function nivelIntermedio() {
  const migas = screen.getByRole("navigation", { name: /breadcrumb|migas|ruta/i });
  return within(migas).getAllByRole("link");
}

describe("DetallePedidoPage — breadcrumb según el origen", () => {
  beforeEach(() => {
    estado.permisos = [];
    estado.pedido = PEDIDO;
  });

  it("desde Mis pedidos muestra Mis pedidos", () => {
    estado.permisos = ["designaciones.gestionar"];
    renderDetalle("mis-pedidos");

    expect(
      screen.getByRole("navigation", { name: /breadcrumb|migas|ruta/i }),
    ).not.toHaveTextContent("Designaciones");
    const enlace = nivelIntermedio().find((a) => a.textContent === "Mis pedidos");
    expect(enlace).toHaveAttribute("href", "/designaciones/mis-pedidos");
    expect(screen.queryByRole("link", { name: "Revisión" })).not.toBeInTheDocument();
  });

  it("desde Revisión muestra Revisión", () => {
    estado.permisos = ["designaciones.revisar"];
    renderDetalle("revision");

    expect(screen.getByRole("link", { name: "Revisión" })).toHaveAttribute(
      "href",
      "/designaciones/revision",
    );
  });

  it("con link directo y sin permiso de revisión usa Mis pedidos", () => {
    estado.permisos = ["designaciones.gestionar"];
    renderDetalle();

    expect(screen.getByRole("link", { name: "Mis pedidos" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Revisión" })).not.toBeInTheDocument();
  });

  it("con link directo y permiso de revisión usa Revisión", () => {
    estado.permisos = ["designaciones.revisar"];
    renderDetalle();

    expect(screen.getByRole("link", { name: "Revisión" })).toBeInTheDocument();
  });

  it("el link de error vuelve al origen", () => {
    estado.pedido = undefined;
    estado.permisos = ["designaciones.gestionar"];
    renderDetalle("mis-pedidos");

    expect(screen.getByRole("link", { name: "Volver a Mis pedidos" })).toHaveAttribute(
      "href",
      "/designaciones/mis-pedidos",
    );
  });
});

describe("DetallePedidoPage — encabezado", () => {
  beforeEach(() => {
    estado.permisos = ["designaciones.revisar"];
    estado.pedido = PEDIDO;
  });

  it("titula con el tipo de novedad y muestra solo el período como meta", () => {
    renderDetalle("revision");

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(
      /^Cambio de cargo o dedicación$/,
    );
    expect(
      screen.getByText("Segundo cuatrimestre 2026", { selector: ".meta" }),
    ).toBeInTheDocument();
    expect(screen.queryByText(/2026-9005/)).not.toBeInTheDocument();
  });
});
