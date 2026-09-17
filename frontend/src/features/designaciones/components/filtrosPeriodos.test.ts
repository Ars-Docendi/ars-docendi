import { describe, expect, it } from "vitest";
import type { PeriodoDesignacion } from "../types";
import {
  aplicarFiltrosPeriodos,
  aplicarFiltrosYOrdenPeriodos,
  FILTROS_PERIODOS_INICIALES,
  siguienteOrdenPeriodos,
} from "./filtrosPeriodos";

function periodo(id: string, overrides: Partial<PeriodoDesignacion> = {}): PeriodoDesignacion {
  return {
    id,
    nombre: "Primer cuatrimestre",
    cargaDesde: "2026-01-01",
    cargaHasta: "2026-02-28",
    impactoDesde: "2026-03-01",
    impactoHasta: "2026-07-31",
    activo: true,
    ...overrides,
  };
}

describe("filtros de Períodos", () => {
  it("busca nombres sin distinguir mayúsculas ni tildes", () => {
    expect(
      aplicarFiltrosPeriodos([periodo("1", { nombre: "Período académico" })], {
        ...FILTROS_PERIODOS_INICIALES,
        nombre: "academico",
      }),
    ).toHaveLength(1);
  });

  it("permite seleccionar Activo, Inactivo o ambos", () => {
    const periodos = [periodo("a"), periodo("i", { activo: false })];
    expect(
      aplicarFiltrosPeriodos(periodos, { ...FILTROS_PERIODOS_INICIALES, activo: ["activo"] }),
    ).toEqual([periodos[0]]);
    expect(
      aplicarFiltrosPeriodos(periodos, {
        ...FILTROS_PERIODOS_INICIALES,
        activo: ["activo", "inactivo"],
      }),
    ).toHaveLength(2);
  });

  it("ordena las fechas cronológicamente y vuelve al impacto descendente por defecto", () => {
    const enero = periodo("enero", { impactoDesde: "2026-01-01" });
    const marzo = periodo("marzo", { impactoDesde: "2026-03-01" });
    expect(
      aplicarFiltrosYOrdenPeriodos([marzo, enero], FILTROS_PERIODOS_INICIALES, {
        columna: "impactoDesde",
        direccion: "asc",
      }).map((item) => item.id),
    ).toEqual(["enero", "marzo"]);
    expect(
      aplicarFiltrosYOrdenPeriodos([enero, marzo], FILTROS_PERIODOS_INICIALES, null).map(
        (item) => item.id,
      ),
    ).toEqual(["marzo", "enero"]);
  });

  it("cicla ascendente, descendente y sin orden", () => {
    expect(siguienteOrdenPeriodos(null, "nombre")).toEqual({
      columna: "nombre",
      direccion: "asc",
    });
    expect(siguienteOrdenPeriodos({ columna: "nombre", direccion: "asc" }, "nombre")).toEqual({
      columna: "nombre",
      direccion: "desc",
    });
    expect(siguienteOrdenPeriodos({ columna: "nombre", direccion: "desc" }, "nombre")).toBeNull();
  });
});
