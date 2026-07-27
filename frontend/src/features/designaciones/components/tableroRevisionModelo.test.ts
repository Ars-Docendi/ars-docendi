import { describe, it, expect } from "vitest";
import {
  avanceEtapaRetorno,
  avancePedido,
  construirColumnas,
  esTuTurno,
  motivoRechazo,
  quienDevolvio,
  rolDeQuienDevolvio,
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

function evento(
  accion: AccionHistorial,
  opts: {
    comentario?: string;
    fecha?: string;
    porNombre?: string;
    porRol?: EventoHistorial["porRol"];
  } = {},
): EventoHistorial {
  contador += 1;
  return {
    id: `e${contador}`,
    accion,
    porRol: opts.porRol ?? "Coordinador",
    porNombre: opts.porNombre ?? "M. Díaz",
    etapa: "en_revision_coordinador",
    comentario: opts.comentario,
    fecha: opts.fecha ?? "2026-01-01T00:00:00.000Z",
  };
}

describe("construirColumnas (secciones por etapa del circuito)", () => {
  it("distribuye los pedidos en las 4 secciones por etapa; un devuelto vive en la sección de su etapaRetorno", () => {
    const secciones = construirColumnas([
      pedido("en_revision_coordinador"),
      pedido("en_revision_secretaria"),
      pedido("en_revision_decanato"),
      pedido("en_lote"),
      pedido("rechazado"),
      pedido("devuelto", {
        etapaRetorno: "en_revision_secretaria",
        propietarioActual: "Coordinador",
      }),
    ]);

    expect(secciones.map((s) => s.id)).toEqual([
      "en-coordinacion",
      "en-secretaria",
      "en-decanato",
      "finalizados",
    ]);
    const porId = Object.fromEntries(secciones.map((s) => [s.id, s.pedidos.length]));
    expect(porId).toEqual({
      "en-coordinacion": 1,
      "en-secretaria": 2,
      "en-decanato": 1,
      finalizados: 2,
    });
  });

  it("ordena cada sección de etapa: prioritarios primero, después devueltos, después por fecha (el que espera hace más tiempo, arriba)", () => {
    const reciente = pedido("en_revision_coordinador", {
      docente: { dni: "r", nombre: "Reciente", antiguedad: 3 },
      historial: [evento("crear", { fecha: "2026-03-01T00:00:00.000Z" })],
    });
    const antiguo = pedido("en_revision_coordinador", {
      docente: { dni: "a", nombre: "Antiguo", antiguedad: 3 },
      historial: [evento("crear", { fecha: "2026-01-01T00:00:00.000Z" })],
    });
    const devuelto = pedido("devuelto", {
      docente: { dni: "d", nombre: "Devuelto", antiguedad: 3 },
      etapaRetorno: "en_revision_coordinador",
      propietarioActual: "Jefe de Cátedra",
      historial: [evento("devolver", { fecha: "2026-02-01T00:00:00.000Z", porNombre: "S. Gómez" })],
    });
    const prioritario = pedido("en_revision_coordinador", {
      docente: { dni: "p", nombre: "Prioritario", antiguedad: 3 },
      prioritario: true,
      historial: [evento("crear", { fecha: "2026-04-01T00:00:00.000Z" })],
    });

    const [enCoordinacion] = construirColumnas([reciente, antiguo, devuelto, prioritario]);
    expect(enCoordinacion.pedidos.map((p) => p.docente.nombre)).toEqual([
      "Prioritario",
      "Devuelto",
      "Antiguo",
      "Reciente",
    ]);
  });

  it("en Finalizados, Aceptados van antes que Rechazados; dentro de cada bloque, el cierre más reciente arriba", () => {
    const aceptadoViejo = pedido("en_lote", {
      docente: { dni: "av", nombre: "Aceptado Viejo", antiguedad: 3 },
      historial: [evento("aceptar", { fecha: "2026-01-01T00:00:00.000Z" })],
    });
    const aceptadoNuevo = pedido("en_lote", {
      docente: { dni: "an", nombre: "Aceptado Nuevo", antiguedad: 3 },
      historial: [evento("aceptar", { fecha: "2026-03-01T00:00:00.000Z" })],
    });
    const rechazado = pedido("rechazado", {
      docente: { dni: "rz", nombre: "Rechazado", antiguedad: 3 },
      historial: [evento("rechazar", { fecha: "2026-02-01T00:00:00.000Z" })],
    });

    const secciones = construirColumnas([rechazado, aceptadoViejo, aceptadoNuevo]);
    const finalizados = secciones.find((s) => s.id === "finalizados");
    expect(finalizados?.pedidos.map((p) => p.docente.nombre)).toEqual([
      "Aceptado Nuevo",
      "Aceptado Viejo",
      "Rechazado",
    ]);
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

describe("motivoRechazo (motivo crudo para la cita destacada del detalle)", () => {
  it("devuelve el comentario del último rechazo, sin el prefijo 'Rechazado:'", () => {
    const rechazado = pedido("rechazado", {
      historial: [evento("rechazar", { comentario: "cargo no presupuestado para el período" })],
    });
    expect(motivoRechazo(rechazado)).toBe("cargo no presupuestado para el período");
  });

  it("es undefined cuando el rechazo no registró comentario", () => {
    expect(motivoRechazo(pedido("rechazado"))).toBeUndefined();
  });
});

describe("quienDevolvio / rolDeQuienDevolvio (para 'Devuelto por {nombre} ({rol})')", () => {
  it("devuelve el nombre y el rol de quien devolvió el pedido por última vez", () => {
    const devuelto = pedido("devuelto", {
      etapaRetorno: "en_revision_coordinador",
      historial: [
        evento("devolver", {
          porNombre: "S. Gómez",
          porRol: "Secretaría",
          comentario: "falta adjunto",
        }),
      ],
    });
    expect(quienDevolvio(devuelto)).toBe("S. Gómez");
    expect(rolDeQuienDevolvio(devuelto)).toBe("Secretaría");
  });

  it("son undefined cuando no hay evento de devolución en el historial", () => {
    expect(quienDevolvio(pedido("en_revision_coordinador"))).toBeUndefined();
    expect(rolDeQuienDevolvio(pedido("en_revision_coordinador"))).toBeUndefined();
  });
});

describe("avanceEtapaRetorno (el mismo stepper + etapa · x/4 que un estado en revisión, para un Devuelto)", () => {
  it("calcula el avance sobre etapaRetorno, no sobre estado", () => {
    const devuelto = pedido("devuelto", { etapaRetorno: "en_revision_decanato" });
    expect(avanceEtapaRetorno(devuelto)).toEqual({ etiqueta: "En Decanato", paso: 3, total: 4 });
  });

  it("es null cuando el pedido no tiene etapaRetorno", () => {
    expect(avanceEtapaRetorno(pedido("en_revision_coordinador"))).toBeNull();
  });
});
