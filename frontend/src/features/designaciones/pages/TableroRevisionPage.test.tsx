import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { PeriodoDesignacion } from "../types";
import { TableroRevisionPage } from "./TableroRevisionPage";

const PERIODO: PeriodoDesignacion = {
  id: "periodo-1",
  nombre: "Segundo cuatrimestre 2026",
  cargaDesde: "2026-06-01",
  cargaHasta: "2026-07-31",
  impactoDesde: "2026-08-01",
  impactoHasta: "2026-12-31",
  activo: true,
};

const estado = vi.hoisted(() => ({
  permisos: [] as string[],
  periodoActivo: null as PeriodoDesignacion | null,
}));
const exportarLote = vi.hoisted(() => vi.fn());

vi.mock("../../../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({ user: { permissions: estado.permisos } }),
}));
vi.mock("../hooks/useActorContexto", () => ({
  useActorContexto: () => ({ rol: "Secretaría", nombre: "L. Fernández" }),
}));
vi.mock("../hooks/usePedidos", () => ({
  usePedidosPorAmbito: () => ({ data: [], isLoading: false, isError: false, refetch: vi.fn() }),
}));
vi.mock("../hooks/useCatalogosDesignaciones", () => ({
  useCatalogosDesignaciones: () => ({
    data: { periodoActivo: estado.periodoActivo, periodos: [] },
    isLoading: false,
    refetch: vi.fn(),
  }),
}));
vi.mock("../api/loteApi", () => ({
  exportarLote,
  esIntegridadComprometida: async () => false,
}));

function montar() {
  render(<TableroRevisionPage />, { wrapper: MemoryRouter });
}

beforeEach(() => {
  estado.permisos = [];
  estado.periodoActivo = PERIODO;
  exportarLote.mockReset();
});

describe("TableroRevisionPage: Exportar lote", () => {
  it("sin designaciones.exportar_lote no ofrece la exportación, aunque el rol sea departamental", () => {
    montar();
    expect(screen.queryByRole("button", { name: "Exportar lote" })).not.toBeInTheDocument();
    expect(screen.queryByText(/Período:/)).not.toBeInTheDocument();
  });

  it("con el permiso, es la acción del encabezado y nombra el período que descarga", async () => {
    estado.permisos = ["designaciones.exportar_lote"];
    exportarLote.mockResolvedValue(new Blob());
    URL.createObjectURL = vi.fn(() => "blob:lote");
    URL.revokeObjectURL = vi.fn();
    montar();

    expect(screen.getByText(/Período: Segundo cuatrimestre 2026/)).toBeInTheDocument();
    await userEvent.setup().click(screen.getByRole("button", { name: "Exportar lote" }));
    expect(exportarLote).toHaveBeenCalledWith("periodo-1");
  });

  it("sin período activo deja el botón deshabilitado y explica por qué", () => {
    estado.permisos = ["designaciones.exportar_lote"];
    estado.periodoActivo = null;
    montar();

    const boton = screen.getByRole("button", { name: "Exportar lote" });
    expect(boton).toBeDisabled();
    expect(boton).toHaveAccessibleDescription(
      "Sin período activo: configuralo para descargar el lote.",
    );
  });

  it("si la descarga falla lo informa y deja reintentar", async () => {
    estado.permisos = ["designaciones.exportar_lote"];
    exportarLote.mockRejectedValue(new Error("red"));
    montar();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Exportar lote" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("No se descargó el lote");
    await user.click(screen.getByRole("button", { name: "Exportar lote" }));
    expect(exportarLote).toHaveBeenCalledTimes(2);
  });
});
