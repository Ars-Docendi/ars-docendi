import { describe, it, expect } from "vitest";
import { validarPedido } from "./pedidoValidacion";
import type { Adjunto, DatosEditablesPedido, PedidoDesignacion } from "./types";

function datosBase(overrides: Partial<DatosEditablesPedido> = {}): DatosEditablesPedido {
  return {
    docente: { dni: "30111222", nombre: "Ana Pérez", antiguedad: 5 },
    materiaAsociada: "Ingeniería de Software",
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Sin novedad",
    haceHorasOtroDepto: false,
    adjuntos: [],
    ...overrides,
  };
}

function pedidoExistente(dni: string, id = "otro"): PedidoDesignacion {
  return {
    id,
    periodoId: "1",
    catedra: "Ingeniería de Software",
    carrera: "Ingeniería en Informática",
    docente: { dni, nombre: "Existente", antiguedad: 3 },
    materiaAsociada: "Ingeniería de Software",
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Sin novedad",
    haceHorasOtroDepto: false,
    adjuntos: [],
    estado: "borrador",
    prioritario: false,
    historial: [],
  };
}

const ADJUNTOS_ALTA: Adjunto[] = [
  { id: "1", nombre: "cv.pdf", tipo: "cv" },
  { id: "2", nombre: "frente.jpg", tipo: "dni_frente" },
  { id: "3", nombre: "dorso.jpg", tipo: "dni_dorso" },
];

describe("validarPedido", () => {
  it("un 'Sin novedad' completo no tiene errores", () => {
    const errores = validarPedido(datosBase(), { pedidosExistentes: [] });
    expect(Object.keys(errores)).toHaveLength(0);
  });

  describe("BR-designaciones-001 — un pedido por docente por período", () => {
    it("unPedidoPorDocentePorPeriodo — marca duplicado", () => {
      const errores = validarPedido(datosBase(), {
        pedidosExistentes: [pedidoExistente("30111222")],
      });
      expect(errores.docente).toBeTruthy();
    });

    it("al editar el propio pedido no se marca como duplicado", () => {
      const errores = validarPedido(datosBase(), {
        pedidosExistentes: [pedidoExistente("30111222", "p1")],
        pedidoActualId: "p1",
      });
      expect(errores.docente).toBeUndefined();
    });
  });

  describe("BR-designaciones-002 — Alta exige CV + DNI frente + DNI dorso", () => {
    it("altaExigeCvYDniFrenteYDorso — falta alguno", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Alta",
          cargoSolicitado: "Ayudante",
          dedicacionSolicitada: "Categoría 5",
          adjuntos: [{ id: "1", nombre: "cv.pdf", tipo: "cv" }],
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.adjuntos).toBeTruthy();
    });

    it("Alta con los tres adjuntos no marca error de adjuntos", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Alta",
          cargoSolicitado: "Ayudante",
          dedicacionSolicitada: "Categoría 5",
          adjuntos: ADJUNTOS_ALTA,
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.adjuntos).toBeUndefined();
    });
  });

  describe("BR-designaciones-003 — Baja exige justificativo", () => {
    it("bajaExigeJustificativo — sin adjunto", () => {
      const errores = validarPedido(datosBase({ novedad: "Baja", adjuntos: [] }), {
        pedidosExistentes: [],
      });
      expect(errores.adjuntos).toBeTruthy();
    });

    it("Baja con justificativo no marca error", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Baja",
          adjuntos: [{ id: "1", nombre: "j.pdf", tipo: "justificativo" }],
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.adjuntos).toBeUndefined();
    });
  });

  describe("BR-designaciones-004 — Cambio exige justificación", () => {
    it("cambioExigeJustificacion — justificación vacía", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Cambio de cargo o dedicación",
          cargoSolicitado: "Adjunto",
          dedicacionSolicitada: "Categoría 3",
          justificacion: "   ",
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.justificacion).toBeTruthy();
    });

    it("Cambio con justificación no marca ese error", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Cambio de cargo o dedicación",
          cargoSolicitado: "Adjunto",
          dedicacionSolicitada: "Categoría 3",
          justificacion: "Aumento de carga de investigación.",
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.justificacion).toBeUndefined();
    });
  });
});
