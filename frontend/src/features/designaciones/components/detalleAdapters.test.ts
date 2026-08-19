import { describe, it, expect } from "vitest";
import {
  accionAAuditVerb,
  derivarCadena,
  historialAAuditEntries,
  posicionEtapa,
} from "./detalleAdapters";
import type { ActorContexto, EstadoPedido, EventoHistorial, PedidoDesignacion } from "../types";

function pedido(overrides: Partial<PedidoDesignacion> = {}): PedidoDesignacion {
  return {
    id: "p1",
    periodoId: "1",
    catedra: "Ingeniería de Software",
    carrera: "Ingeniería en Informática",
    docente: { dni: "30111222", nombre: "Ana Pérez", antiguedad: 5 },
    asignaciones: [{ materia: "Ingeniería de Software", horas: 6 }],
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Cambio de cargo o dedicación",
    horasExternas: 0,
    horasInvestigacion: 0,
    esAgenteExterno: false,
    adjuntos: [],
    estado: "en_revision_secretaria",
    prioritario: false,
    historial: [],
    ...overrides,
  };
}

const ACTOR: ActorContexto = {
  rol: "Secretaría",
  nombre: "Demo",
  carrera: "Ingeniería en Informática",
};

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

describe("derivarCadena — estado/historial → 5 etapas (Jefe de Cátedra → … → En lote)", () => {
  function estados(etapas: ReturnType<typeof derivarCadena>) {
    return etapas.map((e) => e.estado);
  }

  it("en revisión de Secretaría: JC y Coordinador done, Secretaría current, Decanato y En lote pending", () => {
    const etapas = derivarCadena(pedido({ estado: "en_revision_secretaria" }), ACTOR);
    expect(etapas.map((e) => e.rol)).toEqual([
      "Jefe de Cátedra",
      "Coordinador",
      "Secretaría",
      "Decanato",
      "En lote",
    ]);
    expect(estados(etapas)).toEqual(["cumplida", "cumplida", "actual", "pendiente", "pendiente"]);
  });

  it("marca '· vos' en la etapa que ocupa el actor actual", () => {
    const etapas = derivarCadena(pedido({ estado: "en_revision_secretaria" }), ACTOR);
    const secretaria = etapas.find((e) => e.rol === "Secretaría");
    expect(secretaria?.esVos).toBe(true);
    expect(secretaria?.detalle).toBe("En revisión · vos");
  });

  it("en_lote: toda la cadena queda done", () => {
    const etapas = derivarCadena(pedido({ estado: "en_lote" }), ACTOR);
    expect(estados(etapas)).toEqual(["cumplida", "cumplida", "cumplida", "cumplida", "cumplida"]);
  });

  it("rechazado: la etapa del rol que rechazó queda rejected", () => {
    const etapas = derivarCadena(
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
      ACTOR,
    );
    expect(estados(etapas)).toEqual([
      "cumplida",
      "cumplida",
      "rechazada",
      "pendiente",
      "pendiente",
    ]);
  });

  it("devuelto: la etapa de retorno queda returned", () => {
    const etapas = derivarCadena(
      pedido({
        estado: "devuelto" as EstadoPedido,
        etapaRetorno: "en_revision_secretaria",
        propietarioActual: "Coordinador",
      }),
      ACTOR,
    );
    expect(estados(etapas)).toEqual(["cumplida", "cumplida", "devuelta", "pendiente", "pendiente"]);
  });

  it("rechazo de Administración: la X cae en la última etapa de revisión, no en Coordinador", () => {
    const historial: EventoHistorial[] = [
      {
        id: "e1",
        accion: "enviar",
        porRol: "Jefe de Cátedra",
        porNombre: "G. Ruiz",
        etapa: "en_revision_coordinador",
        fecha: "2026-03-01T10:00:00.000Z",
      },
      {
        id: "e2",
        accion: "aceptar",
        porRol: "Coordinador",
        porNombre: "M. Díaz",
        etapa: "en_revision_secretaria",
        fecha: "2026-03-02T10:00:00.000Z",
      },
      {
        id: "e3",
        accion: "aceptar",
        porRol: "Secretaría",
        porNombre: "L. Sosa",
        etapa: "en_revision_decanato",
        fecha: "2026-03-03T10:00:00.000Z",
      },
      {
        id: "e4",
        accion: "rechazar",
        porRol: "Administración",
        porNombre: "A. Ramos",
        etapa: "rechazado",
        comentario: "Documentación inconsistente.",
        fecha: "2026-03-04T10:00:00.000Z",
      },
    ];
    const etapas = derivarCadena(pedido({ estado: "rechazado", historial }), ACTOR);
    expect(estados(etapas)).toEqual(["cumplida", "cumplida", "cumplida", "rechazada", "pendiente"]);
  });

  it("borrador: la etapa del Jefe de Cátedra se rotula 'En borrador', no 'En revisión'", () => {
    const [jefe] = derivarCadena(pedido({ estado: "borrador" }), ACTOR);
    expect(jefe.estado).toBe("actual");
    expect(jefe.detalle).toBe("En borrador");
  });
});

describe("posicionEtapa — posición de la etapa de revisión en la cadena", () => {
  it("ubica cada etapa de revisión sobre 4 (Coordinador = 2 de 4)", () => {
    expect(posicionEtapa("en_revision_coordinador")).toEqual({ n: 2, total: 4 });
    expect(posicionEtapa("en_revision_secretaria")).toEqual({ n: 3, total: 4 });
    expect(posicionEtapa("en_revision_decanato")).toEqual({ n: 4, total: 4 });
  });

  it("devuelve null fuera de las etapas de revisión", () => {
    expect(posicionEtapa("borrador")).toBeNull();
    expect(posicionEtapa("en_lote")).toBeNull();
    expect(posicionEtapa("rechazado")).toBeNull();
  });
});
