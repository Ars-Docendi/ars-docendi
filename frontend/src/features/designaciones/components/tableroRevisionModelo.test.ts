import { describe, it, expect } from "vitest";
import {
  avancePedido,
  construirColumnas,
  detallePedido,
  esTuTurno,
  motivoRechazo,
} from "./tableroRevisionModelo";
import type {
  AccionHistorial,
  ActorContexto,
  EstadoPedido,
  EventoHistorial,
  PedidoDesignacion,
} from "../types";

const CARRERA = "Ingeniería en Informática";
const COORD: ActorContexto = { rol: "Coordinador", nombre: "M. Díaz", carrera: CARRERA };

let contador = 0;
function pedido(
  estado: EstadoPedido,
  overrides: Partial<PedidoDesignacion> = {},
): PedidoDesignacion {
  contador += 1;
  return {
    id: `m${contador}`,
    periodoId: "1",
    catedra: "Cátedra X",
    carrera: CARRERA,
    docente: { dni: `${contador}`, nombre: `Docente ${contador}`, antiguedad: 3 },
    asignaciones: [{ materia: "Materia X", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Sin novedad",
    horasExternas: 0,
    horasInvestigacion: 0,
    adjuntos: [],
    estado,
    prioritario: false,
    historial: [],
    ...overrides,
  };
}

function evento(accion: AccionHistorial, comentario?: string): EventoHistorial {
  return {
    id: `e${contador}`,
    accion,
    porRol: "Coordinador",
    porNombre: "M. Díaz",
    etapa: "en_revision_coordinador",
    comentario,
    fecha: "2026-01-01T00:00:00.000Z",
  };
}

describe("construirColumnas (modelo D)", () => {
  it("devuelve las 4 columnas de avance y rutea cada pedido por su estado", () => {
    const columnas = construirColumnas(
      [
        pedido("en_revision_coordinador"),
        pedido("en_revision_decanato"),
        pedido("en_lote"),
        pedido("devuelto"),
        pedido("rechazado"),
      ],
      COORD,
    );

    expect(columnas.map((c) => c.id)).toEqual([
      "en-revision",
      "aceptados",
      "devueltos",
      "rechazados",
    ]);
    const porId = Object.fromEntries(columnas.map((c) => [c.id, c.pedidos.length]));
    expect(porId).toEqual({ "en-revision": 2, aceptados: 1, devueltos: 1, rechazados: 1 });
  });

  it("ordena la columna En revisión poniendo primero los pedidos en turno del actor", () => {
    const ajeno = pedido("en_revision_secretaria");
    const mio = pedido("en_revision_coordinador");
    const [enRevision] = construirColumnas([ajeno, mio], COORD);
    expect(enRevision.pedidos.map((p) => p.id)).toEqual([mio.id, ajeno.id]);
  });
});

describe("avancePedido (x/4)", () => {
  it("mapea cada etapa de la cadena a su paso", () => {
    expect(avancePedido(pedido("en_revision_coordinador"))).toEqual({
      etiqueta: "En Coordinación",
      paso: 1,
      total: 4,
    });
    expect(avancePedido(pedido("en_revision_secretaria"))?.paso).toBe(2);
    expect(avancePedido(pedido("en_revision_decanato"))?.paso).toBe(3);
    expect(avancePedido(pedido("en_lote"))).toEqual({ etiqueta: "En lote", paso: 4, total: 4 });
  });

  it("no aplica a estados terminales sin avance (devuelto / rechazado)", () => {
    expect(avancePedido(pedido("devuelto"))).toBeNull();
    expect(avancePedido(pedido("rechazado"))).toBeNull();
  });
});

describe("esTuTurno", () => {
  it("es el turno del Coordinador en su etapa y ámbito, no en etapas ajenas", () => {
    expect(esTuTurno(pedido("en_revision_coordinador"), COORD)).toBe(true);
    expect(esTuTurno(pedido("en_revision_secretaria"), COORD)).toBe(false);
    expect(esTuTurno(pedido("en_revision_coordinador", { carrera: "Otra carrera" }), COORD)).toBe(
      false,
    );
  });
});

describe("detallePedido (motivo de devolución / rechazo)", () => {
  it("muestra el motivo del último evento de devolución y de rechazo", () => {
    const devuelto = pedido("devuelto", {
      historial: [evento("devolver", "falta el aval del área")],
    });
    const rechazado = pedido("rechazado", {
      historial: [evento("rechazar", "cargo no presupuestado")],
    });
    expect(detallePedido(devuelto)).toBe("Devuelto: falta el aval del área");
    expect(detallePedido(rechazado)).toBe("Rechazado: cargo no presupuestado");
  });
});

describe("motivoRechazo (motivo crudo para la cita de la card)", () => {
  it("devuelve el comentario del último rechazo, sin el prefijo 'Rechazado:'", () => {
    const rechazado = pedido("rechazado", {
      historial: [evento("rechazar", "cargo no presupuestado para el período")],
    });
    expect(motivoRechazo(rechazado)).toBe("cargo no presupuestado para el período");
  });

  it("es undefined cuando el rechazo no registró comentario", () => {
    expect(motivoRechazo(pedido("rechazado"))).toBeUndefined();
  });
});
