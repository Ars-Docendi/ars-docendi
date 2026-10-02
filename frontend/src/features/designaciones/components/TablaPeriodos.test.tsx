import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { PeriodoDesignacion } from "../types";
import { TablaPeriodos } from "./TablaPeriodos";

const PERIODOS: PeriodoDesignacion[] = [
  {
    id: "marzo",
    nombre: "Segundo cuatrimestre",
    cargaDesde: "2026-06-01",
    cargaHasta: "2026-07-31",
    impactoDesde: "2026-08-01",
    impactoHasta: "2026-12-31",
    activo: true,
  },
  {
    id: "enero",
    nombre: "Primer cuatrimestre",
    cargaDesde: "2026-01-01",
    cargaHasta: "2026-02-28",
    impactoDesde: "2026-03-01",
    impactoHasta: "2026-07-31",
    activo: false,
  },
];

describe("TablaPeriodos", () => {
  it("filtra desde encabezados, limpia individualmente y deja Acciones sin control", async () => {
    const user = userEvent.setup();
    render(<TablaPeriodos periodos={PERIODOS} onEditar={vi.fn()} onEliminar={vi.fn()} />);

    expect(screen.getByRole("columnheader", { name: "Acciones" })).not.toHaveAttribute("aria-sort");
    expect(screen.queryByRole("button", { name: "Filtrar Acciones" })).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Filtrar Nombre" }));
    await user.type(screen.getByRole("textbox", { name: "Buscar Nombre" }), "segundo");
    expect(screen.getByText("Segundo cuatrimestre")).toBeInTheDocument();
    expect(screen.queryByText("Primer cuatrimestre")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Limpiar filtro" }));
    expect(screen.getByText("Primer cuatrimestre")).toBeInTheDocument();
  });

  it("permite estado múltiple y el ciclo accesible de orden", async () => {
    const user = userEvent.setup();
    render(<TablaPeriodos periodos={PERIODOS} onEditar={vi.fn()} onEliminar={vi.fn()} />);

    await user.click(screen.getByRole("button", { name: "Filtrar Activo" }));
    await user.click(screen.getByLabelText("Inactivo"));
    expect(screen.getByText("Primer cuatrimestre")).toBeInTheDocument();
    expect(screen.queryByText("Segundo cuatrimestre")).not.toBeInTheDocument();

    const encabezado = screen.getByRole("columnheader", { name: "Impacto desde" });
    await user.click(encabezado);
    expect(encabezado).toHaveAttribute("aria-sort", "ascending");
    await user.click(encabezado);
    expect(encabezado).toHaveAttribute("aria-sort", "descending");
    await user.click(encabezado);
    expect(encabezado).toHaveAttribute("aria-sort", "none");
  });
});
