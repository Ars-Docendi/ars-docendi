import { describe, expect, it } from "vitest";
import {
  FORMULARIO_VACIO,
  hayCambios,
  validarNuevaSolicitud,
  type FormularioNuevaSolicitud,
} from "./validacionNuevaSolicitud";

const VALIDO: FormularioNuevaSolicitud = {
  materiaId: "m1",
  comision: "K3001",
  dia: "20/11/2026",
  horarioDesde: "08:00",
  horarioHasta: "10:00",
  cantidadAlumnosAprox: "30",
};

describe("validarNuevaSolicitud", () => {
  it("accepts a complete form", () => {
    expect(validarNuevaSolicitud(VALIDO)).toEqual({});
  });

  it("reports every missing field with its own message", () => {
    expect(validarNuevaSolicitud(FORMULARIO_VACIO)).toEqual({
      materiaId: "Elegí una materia.",
      comision: "Ingresá la comisión.",
      dia: "Elegí el día del examen.",
      horarioDesde: "Completá el horario desde.",
      horarioHasta: "Completá el horario hasta.",
      cantidadAlumnosAprox: "Ingresá una cantidad de alumnos mayor a cero.",
    });
  });

  it("distinguishes an impossible date and time from a missing one", () => {
    const errores = validarNuevaSolicitud({
      ...VALIDO,
      dia: "31/02/2026",
      horarioDesde: "25:61",
    });
    expect(errores.dia).toBe("Ingresá una fecha válida (dd/mm/aaaa).");
    expect(errores.horarioDesde).toBe("Ingresá un horario válido (hh:mm).");
  });

  it("requires the end time to be after the start time", () => {
    const errores = validarNuevaSolicitud({
      ...VALIDO,
      horarioDesde: "10:00",
      horarioHasta: "09:00",
    });
    expect(errores.horarioHasta).toBe("El horario hasta debe ser posterior al horario desde.");
  });

  it("rejects zero and negative attendance", () => {
    expect(
      validarNuevaSolicitud({ ...VALIDO, cantidadAlumnosAprox: "0" }).cantidadAlumnosAprox,
    ).toBeDefined();
    expect(
      validarNuevaSolicitud({ ...VALIDO, cantidadAlumnosAprox: "-3" }).cantidadAlumnosAprox,
    ).toBeDefined();
  });

  it("detects unsaved changes", () => {
    expect(hayCambios(FORMULARIO_VACIO)).toBe(false);
    expect(hayCambios({ ...FORMULARIO_VACIO, comision: "K1" })).toBe(true);
  });
});
