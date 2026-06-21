import { describe, it, expect } from "vitest";
import { accionAAuditVerb, derivarTimeline, historialAAuditEntries } from "./detalleAdapters";
import type { EstadoPedido, EventoHistorial, PedidoDesignacion } from "../types";

function pedido(overrides: Partial<PedidoDesignacion> = {}): PedidoDesignacion {
  return {
    id: "p1",
    periodoId: "1",
    catedra: "Ingeniería de Software",
    carrera: "Ingeniería en Informática",
    docente: { dni: "30111222", nombre: "Ana Pérez", antiguedad: 5 },
    materiaAsociada: "Ingeniería de Software",
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Sin novedad",
    haceHorasOtroDepto: false,
    adjuntos: [],
    estado: "en_revision_secretaria",
    prioritario: false,
    historial: [],
    ...overrides,
  };
}

describe("accionAAuditVerb — mapeo español → AuditVerb (símbolo de la lib)", () => {
  it("mapea cada acción a su verbo en inglés", () => {
    expect(accionAAuditVerb("crear")).toBe("create");
    expect(accionAAuditVerb("aceptar")).toBe("approve");
    expect(accionAAuditVerb("rechazar")).toBe("reject");
    expect(accionAAuditVerb("devolver")).toBe("return");
    expect(accionAAuditVerb("editar")).toBe("update");
    expect(accionAAuditVerb("priorizar")).toBe("update");
  });
});

describe("historialAAuditEntries", () => {
  it("traduce el historial a entradas con iniciales, verbo y fecha", () => {
    const historial: EventoHistorial[] = [
      {
        id: "e1",
        accion: "aceptar",
        porRol: "Coordinador",
        porNombre: "M. Díaz",
        etapa: "en_revision_secretaria",
        fecha: "2026-03-06T10:00:00.000Z",
      },
    ];
    const [entrada] = historialAAuditEntries(historial);
    expect(entrada.verb).toBe("approve");
    expect(entrada.initials).toBe("MD");
    expect(entrada.when).toBe("06/03/2026");
    expect(entrada.actor).toBe("M. Díaz");
  });
});

describe("derivarTimeline — estado/historial → TimelineStep[]", () => {
  function estados(pasos: ReturnType<typeof derivarTimeline>) {
    return pasos.map((p) => p.status);
  }

  it("en revisión de Secretaría: Coordinador done, Secretaría current, Decanato pending", () => {
    const pasos = derivarTimeline(pedido({ estado: "en_revision_secretaria" }));
    expect(estados(pasos)).toEqual(["done", "current", "pending"]);
  });

  it("en_lote: toda la cadena done", () => {
    const pasos = derivarTimeline(pedido({ estado: "en_lote" }));
    expect(estados(pasos)).toEqual(["done", "done", "done"]);
  });

  it("rechazado: la etapa del rol que rechazó queda rejected", () => {
    const pasos = derivarTimeline(
      pedido({
        estado: "rechazado",
        historial: [
          {
            id: "e1",
            accion: "rechazar",
            porRol: "Secretaría",
            porNombre: "L. Fernández",
            etapa: "rechazado",
            comentario: "Falta documentación.",
            fecha: "2026-03-09T09:00:00.000Z",
          },
        ],
      }),
    );
    expect(estados(pasos)).toEqual(["done", "rejected", "pending"]);
  });

  it("devuelto: la etapa de retorno queda returned", () => {
    const pasos = derivarTimeline(
      pedido({
        estado: "devuelto" as EstadoPedido,
        etapaRetorno: "en_revision_secretaria",
        propietarioActual: "Coordinador",
      }),
    );
    expect(estados(pasos)).toEqual(["done", "returned", "pending"]);
  });
});
