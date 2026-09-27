import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { FiltrosAuditoria } from "./FiltrosAuditoria";
import type { EstadoFiltrosAuditoria } from "../hooks/useFiltrosAuditoria";

const DEFAULT: EstadoFiltrosAuditoria = {
  q: "",
  periodo: "7d",
  accion: "",
  modulo: "",
  desde: "",
  hasta: "",
  tabla: "",
  clave: "",
  pagina: 1,
  evento: null,
};

function montar(filtros: EstadoFiltrosAuditoria = DEFAULT, extra: Partial<{ total: number }> = {}) {
  const actualizarFiltros = vi.fn();
  const limpiarFiltros = vi.fn();
  const cantidadFiltrosAvanzados = [
    filtros.desde,
    filtros.hasta,
    filtros.tabla,
    filtros.clave,
  ].filter(Boolean).length;
  const difiereDelDefault = JSON.stringify(filtros) !== JSON.stringify(DEFAULT);
  render(
    <FiltrosAuditoria
      filtros={filtros}
      total={extra.total ?? 131}
      actualizarFiltros={actualizarFiltros}
      limpiarFiltros={limpiarFiltros}
      difiereDelDefault={difiereDelDefault}
      cantidadFiltrosAvanzados={cantidadFiltrosAvanzados}
    />,
  );
  return { actualizarFiltros, limpiarFiltros };
}

describe("FiltrosAuditoria", () => {
  it("selección por default: 7 días, Todas, Todos, sin badge y sin «Limpiar filtros»", () => {
    montar();

    expect(screen.getByRole("button", { name: "7 días" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Todas" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Todos" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.queryByRole("button", { name: "Limpiar filtros" })).not.toBeInTheDocument();
    expect(screen.queryByText("1")).not.toBeInTheDocument();
  });

  it("lista exacta de chips de módulo: Todos, Identidad, Designaciones, Portal, Asistente — sin Aulas ni Tareas", () => {
    montar();

    const grupo = screen.getByRole("group", { name: "Módulo" });
    const nombres = ["Todos", "Identidad", "Designaciones", "Portal", "Asistente"];
    for (const nombre of nombres) {
      expect(screen.getByRole("button", { name: nombre })).toBeInTheDocument();
    }
    expect(grupo.querySelectorAll("button")).toHaveLength(nombres.length);
    expect(screen.queryByRole("button", { name: "Aulas" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Tareas" })).not.toBeInTheDocument();
  });

  it("un filtro Desde cuenta en el badge de «Más filtros» y muestra «Limpiar filtros»", () => {
    montar({ ...DEFAULT, desde: "2026-09-01T00:00" });

    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Limpiar filtros" })).toBeInTheDocument();
  });

  it("«Limpiar filtros» llama al callback", async () => {
    const user = userEvent.setup();
    const { limpiarFiltros } = montar({ ...DEFAULT, q: "algo" });

    await user.click(screen.getByRole("button", { name: "Limpiar filtros" }));

    expect(limpiarFiltros).toHaveBeenCalled();
  });

  it("elegir un chip de acción actualiza el filtro", async () => {
    const user = userEvent.setup();
    const { actualizarFiltros } = montar();

    await user.click(screen.getByRole("button", { name: "Cambios" }));

    expect(actualizarFiltros).toHaveBeenCalledWith({ accion: "UPDATE" });
  });

  it("muestra el total de registros", () => {
    montar(DEFAULT, { total: 312 });

    expect(screen.getByText("312")).toBeInTheDocument();
  });
});
