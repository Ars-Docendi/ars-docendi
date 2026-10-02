import { describe, it, expect } from "vitest";
import { validarPeriodo, esPeriodoValido } from "./periodoValidacion";
import type { ContextoValidacionPeriodo, DatosEditablesPeriodo } from "./periodoValidacion";
import type { PeriodoDesignacion } from "./types";

function datosBase(overrides: Partial<DatosEditablesPeriodo> = {}): DatosEditablesPeriodo {
  return {
    nombre: "2do cuatrimestre 2026",
    cargaDesde: "2026-07-01",
    cargaHasta: "2026-07-31",
    impactoDesde: "2026-08-01",
    impactoHasta: "2026-12-31",
    activo: false,
    ...overrides,
  };
}

function contextoBase(
  overrides: Partial<ContextoValidacionPeriodo> = {},
): ContextoValidacionPeriodo {
  return {
    periodosExistentes: [],
    ...overrides,
  };
}

function periodoExistente(overrides: Partial<PeriodoDesignacion> = {}): PeriodoDesignacion {
  return {
    id: "otro",
    nombre: "1er cuatrimestre 2026",
    cargaDesde: "2026-02-01",
    cargaHasta: "2026-02-28",
    impactoDesde: "2026-03-01",
    impactoHasta: "2026-07-31",
    activo: true,
    ...overrides,
  };
}

describe("validarPeriodo", () => {
  it("rechaza cargaHasta anterior a cargaDesde", () => {
    const errores = validarPeriodo(
      datosBase({ cargaDesde: "2026-07-31", cargaHasta: "2026-07-01" }),
      contextoBase(),
    );
    expect(errores.cargaHasta).toBe(
      "La fecha de carga hasta debe ser posterior o igual a la de carga desde.",
    );
    expect(esPeriodoValido(errores)).toBe(false);
  });

  it("rechaza impactoHasta anterior a impactoDesde", () => {
    const errores = validarPeriodo(
      datosBase({ impactoDesde: "2026-12-31", impactoHasta: "2026-08-01" }),
      contextoBase(),
    );
    expect(errores.impactoHasta).toBe(
      "La fecha de impacto hasta debe ser posterior o igual a la de impacto desde.",
    );
    expect(esPeriodoValido(errores)).toBe(false);
  });

  it("rechaza nombre vacío", () => {
    const errores = validarPeriodo(datosBase({ nombre: "  " }), contextoBase());
    expect(errores.nombre).toBe("El nombre del período es obligatorio.");
  });

  it("rechaza impactoDesde vacío", () => {
    const errores = validarPeriodo(datosBase({ impactoDesde: "" }), contextoBase());
    expect(errores.impactoDesde).toBe("La fecha de impacto desde es obligatoria.");
    expect(esPeriodoValido(errores)).toBe(false);
  });

  it("rechaza impactoHasta vacío", () => {
    const errores = validarPeriodo(datosBase({ impactoHasta: "" }), contextoBase());
    expect(errores.impactoHasta).toBe("La fecha de impacto hasta es obligatoria.");
    expect(esPeriodoValido(errores)).toBe(false);
  });

  it("acepta datos válidos sin errores", () => {
    const errores = validarPeriodo(datosBase(), contextoBase());
    expect(esPeriodoValido(errores)).toBe(true);
  });

  it("acepta rangos con la misma fecha de desde y hasta", () => {
    const errores = validarPeriodo(
      datosBase({ cargaDesde: "2026-07-01", cargaHasta: "2026-07-01" }),
      contextoBase(),
    );
    expect(errores.cargaHasta).toBeUndefined();
  });

  it("rechaza activar cuando ya existe otro período activo", () => {
    const errores = validarPeriodo(
      datosBase({ activo: true }),
      contextoBase({ periodosExistentes: [periodoExistente()] }),
    );
    expect(errores.activo).toBe(
      'Ya existe un período activo: "1er cuatrimestre 2026". Desactivalo primero.',
    );
    expect(esPeriodoValido(errores)).toBe(false);
  });

  it("permite activar si no hay ningún otro período activo", () => {
    const errores = validarPeriodo(
      datosBase({ activo: true }),
      contextoBase({ periodosExistentes: [periodoExistente({ activo: false })] }),
    );
    expect(errores.activo).toBeUndefined();
  });

  it("permite guardar activo si el único activo es el propio período en edición", () => {
    const errores = validarPeriodo(
      datosBase({ activo: true }),
      contextoBase({
        periodosExistentes: [periodoExistente({ id: "actual", activo: true })],
        periodoActualId: "actual",
      }),
    );
    expect(errores.activo).toBeUndefined();
  });
});
