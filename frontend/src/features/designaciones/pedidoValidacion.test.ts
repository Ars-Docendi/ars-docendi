import { describe, it, expect } from "vitest";
import { validarPedido } from "./pedidoValidacion";
import type { Adjunto, DatosEditablesPedido, PedidoDesignacion } from "./types";

function datosBase(overrides: Partial<DatosEditablesPedido> = {}): DatosEditablesPedido {
  return {
    docente: { dni: "30111222", nombre: "Ana Pérez", antiguedad: 5, legajo: "1001" },
    asignaciones: [{ materia: "Ingeniería de Software", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Sin novedad",
    horasExternas: 0,
    horasInvestigacion: 0,
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
    asignaciones: [{ materia: "Ingeniería de Software", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Sin novedad",
    horasExternas: 0,
    horasInvestigacion: 0,
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
      const errores = validarPedido(
        datosBase({ novedad: "Baja", tipoBaja: "Renuncia", adjuntos: [] }),
        { pedidosExistentes: [] },
      );
      expect(errores.adjuntos).toBeTruthy();
    });

    it("Baja con justificativo no marca error", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Baja",
          tipoBaja: "Renuncia",
          adjuntos: [{ id: "1", nombre: "j.pdf", tipo: "justificativo" }],
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.adjuntos).toBeUndefined();
    });
  });

  describe("Tipificación de la baja", () => {
    it("exige tipo de baja", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Baja",
          adjuntos: [{ id: "1", nombre: "j.pdf", tipo: "justificativo" }],
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.tipoBaja).toBeTruthy();
    });

    it('"Otro" exige detalle en texto libre', () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Baja",
          tipoBaja: "Otro",
          adjuntos: [{ id: "1", nombre: "j.pdf", tipo: "justificativo" }],
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.tipoBajaDetalle).toBeTruthy();
    });

    it('"Otro" con detalle no marca error', () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Baja",
          tipoBaja: "Otro",
          tipoBajaDetalle: "Cambio de área dentro de la misma universidad.",
          adjuntos: [{ id: "1", nombre: "j.pdf", tipo: "justificativo" }],
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.tipoBajaDetalle).toBeUndefined();
    });
  });

  describe("Materias y horas del pedido", () => {
    it("exige al menos una materia", () => {
      const errores = validarPedido(datosBase({ asignaciones: [] }), { pedidosExistentes: [] });
      expect(errores.asignaciones).toBeTruthy();
    });

    it("en Alta, cada fila exige materia y horas > 0", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Alta",
          cargoSolicitado: "Ayudante",
          dedicacionSolicitada: "Categoría 5",
          asignaciones: [{ materia: "Ingeniería de Software", horas: 0 }],
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.asignaciones).toBeTruthy();
    });

    it("Alta con múltiples materias válidas no marca error", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Alta",
          cargoSolicitado: "Ayudante",
          dedicacionSolicitada: "Categoría 5",
          asignaciones: [
            { materia: "Programación I", horas: 6 },
            { materia: "Programación II", horas: 4 },
          ],
          adjuntos: ADJUNTOS_ALTA,
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.asignaciones).toBeUndefined();
    });

    it("D2 — no valida cierre de horas contra la dedicación", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Alta",
          cargoSolicitado: "Ayudante",
          dedicacionSolicitada: "Categoría 1", // dedicación alta, horas cargadas muy por debajo
          asignaciones: [{ materia: "Ingeniería de Software", horas: 1 }],
          horasInvestigacion: 0,
          horasExternas: 0,
          adjuntos: ADJUNTOS_ALTA,
        }),
        { pedidosExistentes: [] },
      );
      expect(Object.keys(errores)).toHaveLength(0);
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
          dedicacionSolicitada: "Categoría 1", // mejor que la actual (Categoría 3)
          justificacion: "Aumento de carga de investigación.",
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.justificacion).toBeUndefined();
    });
  });

  describe("BR-designaciones-018 — Baja y Cambio exigen legajo del docente", () => {
    it("bajaExigeLegajo — sin legajo", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Baja",
          tipoBaja: "Renuncia",
          adjuntos: [{ id: "1", nombre: "j.pdf", tipo: "justificativo" }],
          docente: { dni: "30111222", nombre: "Ana Pérez", antiguedad: 5 },
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.docente).toBeTruthy();
    });

    it("cambioExigeLegajo — sin legajo", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Cambio de cargo o dedicación",
          cargoSolicitado: "Adjunto",
          dedicacionSolicitada: "Categoría 1",
          justificacion: "Motivo.",
          docente: { dni: "30111222", nombre: "Ana Pérez", antiguedad: 5 },
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.docente).toBeTruthy();
    });

    it("Baja con legajo no marca error de docente", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Baja",
          tipoBaja: "Renuncia",
          adjuntos: [{ id: "1", nombre: "j.pdf", tipo: "justificativo" }],
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.docente).toBeUndefined();
    });

    it("en Alta no aplica la restricción (el docente todavía no tiene legajo)", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Alta",
          cargoActual: null,
          dedicacionActual: null,
          cargoSolicitado: "Ayudante",
          dedicacionSolicitada: "Categoría 5",
          adjuntos: ADJUNTOS_ALTA,
          docente: { dni: "30111222", nombre: "Ana Pérez", antiguedad: 0 },
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.docente).toBeUndefined();
    });
  });

  describe("Dedicación solicitada en Cambio solo puede mejorar (D-7)", () => {
    it("rechaza una dedicación solicitada igual a la actual", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Cambio de cargo o dedicación",
          cargoSolicitado: "Adjunto",
          dedicacionSolicitada: "Categoría 3", // igual a dedicacionActual
          justificacion: "Motivo.",
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.dedicacionSolicitada).toBeTruthy();
    });

    it("rechaza una dedicación solicitada peor que la actual", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Cambio de cargo o dedicación",
          cargoSolicitado: "Adjunto",
          dedicacionSolicitada: "Categoría 5", // peor que Categoría 3 (índice mayor)
          justificacion: "Motivo.",
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.dedicacionSolicitada).toBeTruthy();
    });

    it("acepta una dedicación solicitada estrictamente mejor que la actual", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Cambio de cargo o dedicación",
          cargoSolicitado: "Adjunto",
          dedicacionSolicitada: "Categoría 0", // la mejor posible
          justificacion: "Motivo.",
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.dedicacionSolicitada).toBeUndefined();
    });

    it("en Alta no aplica la restricción (no hay dedicación actual)", () => {
      const errores = validarPedido(
        datosBase({
          novedad: "Alta",
          cargoActual: null,
          dedicacionActual: null,
          cargoSolicitado: "Ayudante",
          dedicacionSolicitada: "Categoría 6",
          adjuntos: ADJUNTOS_ALTA,
        }),
        { pedidosExistentes: [] },
      );
      expect(errores.dedicacionSolicitada).toBeUndefined();
    });
  });
});
