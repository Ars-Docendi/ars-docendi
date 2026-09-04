import { describe, it, expect } from "vitest";
import {
  PESTANIAS,
  esTuTurno,
  areaActual,
  areaQueCorrige,
  etiquetaEstado,
  inicioEnCircuito,
  motivoRechazo,
  ordenarPedidos,
  pedidosDePestania,
  pestaniaInicial,
  siguienteOrden,
  quienDevolvio,
  rolDeQuienDevolvio,
  ultimaActualizacion,
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
    horas: 6,
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Cambio de cargo o dedicación",
    horasExternas: 0,
    horasInvestigacion: 0,
    esAgenteExterno: false,
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

describe("pedidosDePestania (reparto en las 5 pestañas de la Tabla)", () => {
  it("cada pestaña de área junta lo que está HOY ahí; un devuelto va al área que lo corrige", () => {
    const todos = [
      pedido("en_revision_coordinador"),
      pedido("en_revision_secretaria"),
      pedido("en_revision_decanato"),
      pedido("en_lote"),
      pedido("rechazado"),
      // Devuelto desde Secretaría: lo corrige Coordinación [BR-014]. Vuelve a
      // Secretaría al reenviarse, pero mientras tanto lo tiene Coordinación.
      pedido("devuelto", {
        etapaRetorno: "en_revision_secretaria",
        propietarioActual: "Coordinador",
      }),
    ];

    const conteo = Object.fromEntries(
      PESTANIAS.map(({ id }) => [id, pedidosDePestania(todos, id).length]),
    );
    expect(conteo).toEqual({
      todos: 6, // sin agrupar: todo lo que entró al circuito
      "en-catedra": 0,
      "en-coordinacion": 2, // el activo en Coordinación + el devuelto que corrige Coordinación
      "en-secretaria": 1, // solo el activo: el devuelto ya no está acá, lo espera
      "en-decanato": 1,
      finalizados: 2,
    });
  });

  it("un devuelto a la Cátedra cae en 'En Cátedra', no en la etapa que lo devolvió", () => {
    // Coordinación lo devolvió a la Cátedra [BR-014]. Volverá a Coordinación al
    // reenviarse, pero hoy lo tiene la Cátedra y no hay nada que el Coordinador
    // pueda hacer con él.
    const devuelto = pedido("devuelto", {
      docente: { dni: "c", nombre: "En Cátedra", antiguedad: 3 },
      etapaRetorno: "en_revision_coordinador",
      propietarioActual: "Jefe de Cátedra",
    });

    expect(pedidosDePestania([devuelto], "en-catedra").map((p) => p.docente.nombre)).toEqual([
      "En Cátedra",
    ]);
    expect(pedidosDePestania([devuelto], "en-coordinacion")).toEqual([]);
  });

  it("En Decanato nunca tiene devueltos: no hay etapa por encima que devuelva", () => {
    const devueltos = [
      pedido("devuelto", {
        etapaRetorno: "en_revision_coordinador",
        propietarioActual: "Jefe de Cátedra",
      }),
      pedido("devuelto", {
        etapaRetorno: "en_revision_secretaria",
        propietarioActual: "Coordinador",
      }),
      pedido("devuelto", {
        etapaRetorno: "en_revision_decanato",
        propietarioActual: "Secretaría",
      }),
    ];

    expect(pedidosDePestania(devueltos, "en-decanato")).toEqual([]);
  });

  it("Todos junta todo lo que entró al circuito, sin los borradores", () => {
    const enviado = pedido("en_revision_coordinador", {
      docente: { dni: "1", nombre: "Enviado", antiguedad: 3 },
    });
    const cancelado = pedido("cancelado", {
      docente: { dni: "2", nombre: "Cancelado", antiguedad: 3 },
    });
    const borrador = pedido("borrador", {
      docente: { dni: "3", nombre: "Borrador", antiguedad: 3 },
    });

    const filas = pedidosDePestania([enviado, cancelado, borrador], "todos");

    // Un borrador todavía no entró al circuito; un cancelado sí, y antes no caía
    // en ninguna sección, así que quedaba invisible en el tablero.
    expect(filas.map((p) => p.docente.nombre).sort()).toEqual(["Cancelado", "Enviado"]);
  });

  it("Finalizados incluye los cancelados, no solo aceptados y rechazados", () => {
    const cancelado = pedido("cancelado", {
      docente: { dni: "9", nombre: "Cancelado Nueve", antiguedad: 3 },
    });

    expect(pedidosDePestania([cancelado], "finalizados").map((p) => p.docente.nombre)).toEqual([
      "Cancelado Nueve",
    ]);
  });

  it("ordena cada pestaña de área: prioritarios, después devueltos, después por fecha (el que espera hace más tiempo, arriba)", () => {
    const reciente = pedido("en_revision_coordinador", {
      docente: { dni: "r", nombre: "Reciente", antiguedad: 3 },
      historial: [evento("crear", { fecha: "2026-03-01T00:00:00.000Z" })],
    });
    const antiguo = pedido("en_revision_coordinador", {
      docente: { dni: "a", nombre: "Antiguo", antiguedad: 3 },
      historial: [evento("crear", { fecha: "2026-01-01T00:00:00.000Z" })],
    });
    // Devuelto desde Secretaría: lo corrige Coordinación, así que cae en esta pestaña.
    const devuelto = pedido("devuelto", {
      docente: { dni: "d", nombre: "Devuelto", antiguedad: 3 },
      etapaRetorno: "en_revision_secretaria",
      propietarioActual: "Coordinador",
      historial: [evento("devolver", { fecha: "2026-02-01T00:00:00.000Z", porNombre: "S. Gómez" })],
    });
    const prioritario = pedido("en_revision_coordinador", {
      docente: { dni: "p", nombre: "Prioritario", antiguedad: 3 },
      prioritario: true,
      historial: [evento("crear", { fecha: "2026-04-01T00:00:00.000Z" })],
    });

    const filas = pedidosDePestania([reciente, antiguo, devuelto, prioritario], "en-coordinacion");
    expect(filas.map((p) => p.docente.nombre)).toEqual([
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

    const filas = pedidosDePestania([rechazado, aceptadoViejo, aceptadoNuevo], "finalizados");
    expect(filas.map((p) => p.docente.nombre)).toEqual([
      "Aceptado Nuevo",
      "Aceptado Viejo",
      "Rechazado",
    ]);
  });
});

describe("pestaniaInicial", () => {
  it("abre en el área propia del actor", () => {
    expect(pestaniaInicial(COORD)).toBe("en-coordinacion");
    expect(pestaniaInicial({ rol: "Secretaría", nombre: "L. F." })).toBe("en-secretaria");
    expect(pestaniaInicial({ rol: "Decanato", nombre: "R. S." })).toBe("en-decanato");
  });

  it("Administración no tiene etapa propia: abre en Todos", () => {
    expect(pestaniaInicial({ rol: "Administración", nombre: "P. G." })).toBe("todos");
  });
});

describe("esTuTurno", () => {
  it("usa las acciones autorizadas por el backend, no el ámbito declarado en UI", () => {
    expect(
      esTuTurno(pedido("en_revision_coordinador", { accionesPermitidas: ["aceptar"] }), COORD),
    ).toBe(true);
    expect(esTuTurno(pedido("en_revision_secretaria", { accionesPermitidas: [] }), COORD)).toBe(
      false,
    );
    expect(
      esTuTurno(
        pedido("en_revision_coordinador", {
          carrera: "Otra carrera",
          accionesPermitidas: ["devolver"],
        }),
        COORD,
      ),
    ).toBe(true);
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

describe("inicioEnCircuito / ultimaActualizacion (fechas de la Tabla)", () => {
  const CREACION = evento("crear", { fecha: "2026-01-05T00:00:00.000Z" });
  const ENVIO = evento("enviar", { fecha: "2026-03-10T00:00:00.000Z" });

  it("el inicio es el primer `enviar`, no el `crear` del borrador", () => {
    const abierto = pedido("en_revision_coordinador", { historial: [CREACION, ENVIO] });

    // El tiempo que el pedido estuvo en borrador no es tiempo de revisión.
    expect(inicioEnCircuito(abierto)).toBe("10/03/2026");
  });

  it("sin `enviar` en el historial no hay inicio de circuito", () => {
    expect(inicioEnCircuito(pedido("borrador", { historial: [CREACION] }))).toBeNull();
  });

  it("la última actualización es el evento más reciente del historial", () => {
    const aceptaCoord = evento("aceptar", { fecha: "2026-03-31T00:00:00.000Z" });
    const enSecretaria = pedido("en_revision_secretaria", {
      historial: [CREACION, ENVIO, aceptaCoord],
    });

    expect(ultimaActualizacion(enSecretaria)).toBe("31/03/2026");
  });
});

describe("areaQueCorrige", () => {
  it("nombra el ÁREA a cargo con el vocabulario de los headers de sección", () => {
    // Un devuelto queda a cargo del área, no de una persona: si vuelve a Secretaría
    // se encarga Secretaría, sea quien sea que lo tome.
    expect(areaQueCorrige(pedido("devuelto", { propietarioActual: "Coordinador" }))).toBe(
      "Coordinación",
    );
    expect(areaQueCorrige(pedido("devuelto", { propietarioActual: "Secretaría" }))).toBe(
      "Secretaría",
    );
    expect(areaQueCorrige(pedido("devuelto", { propietarioActual: "Jefe de Cátedra" }))).toBe(
      "Cátedra",
    );
  });

  it("es null cuando el pedido no declara propietario", () => {
    expect(areaQueCorrige(pedido("devuelto"))).toBeNull();
  });
});

describe("etiquetaEstado / areaActual (Estado y Área son columnas separadas)", () => {
  const DEVOLUCION = evento("devolver", { porNombre: "S. Gómez", porRol: "Secretaría" });

  const devueltoACatedra = () =>
    pedido("devuelto", {
      etapaRetorno: "en_revision_coordinador",
      propietarioActual: "Jefe de Cátedra",
      historial: [DEVOLUCION],
    });

  it("el badge de Estado no lleva el área: eso es la columna Área", () => {
    expect(etiquetaEstado(devueltoACatedra())).toBe("Devuelto");
    expect(etiquetaEstado(pedido("en_revision_coordinador"))).toBe("En revisión");
    expect(etiquetaEstado(pedido("en_revision_decanato"))).toBe("En revisión");
  });

  it("los terminales usan la etiqueta por defecto del StatusBadge", () => {
    expect(etiquetaEstado(pedido("en_lote"))).toBeUndefined();
    expect(etiquetaEstado(pedido("rechazado"))).toBeUndefined();
    expect(etiquetaEstado(pedido("cancelado"))).toBeUndefined();
  });

  it("el área de un pedido en revisión es la que lo revisa", () => {
    expect(areaActual(pedido("en_revision_coordinador"))).toBe("Coordinación");
    expect(areaActual(pedido("en_revision_secretaria"))).toBe("Secretaría");
    expect(areaActual(pedido("en_revision_decanato"))).toBe("Decanato");
  });

  it("el área de un devuelto es la que lo corrige, no la etapa a la que vuelve", () => {
    // Volverá a Coordinación al reenviarse, pero hoy lo tiene la Cátedra.
    expect(areaActual(devueltoACatedra())).toBe("Cátedra");
  });

  it("un pedido cerrado no está en ninguna área del circuito", () => {
    expect(areaActual(pedido("en_lote"))).toBeNull();
    expect(areaActual(pedido("rechazado"))).toBeNull();
    expect(areaActual(pedido("cancelado"))).toBeNull();
  });
});

describe("ordenarPedidos / siguienteOrden", () => {
  const ana = pedido("en_revision_coordinador", {
    docente: { dni: "1", nombre: "Ana Zurita", antiguedad: 3, legajo: "1005" },
    historial: [evento("enviar", { fecha: "2026-03-01T00:00:00.000Z" })],
  });
  const beto = pedido("en_revision_coordinador", {
    docente: { dni: "2", nombre: "Beto Álvarez", antiguedad: 3, legajo: "999" },
    historial: [evento("enviar", { fecha: "2026-01-01T00:00:00.000Z" })],
  });

  it("sin orden manual devuelve la lista tal cual: manda el orden por defecto de la pestaña", () => {
    expect(ordenarPedidos([ana, beto], null)).toEqual([ana, beto]);
  });

  it("ordena por nombre de docente en las dos direcciones", () => {
    // Ordena por el nombre completo tal cual se muestra: "Ana…" antes que "Beto…".
    expect(ordenarPedidos([beto, ana], { columna: "docente", direccion: "asc" })[0]).toBe(ana);
    expect(ordenarPedidos([ana, beto], { columna: "docente", direccion: "desc" })[0]).toBe(beto);
  });

  it("los legajos ordenan como números, no como texto", () => {
    // Como texto, "1005" < "999"; como números es al revés.
    const porLegajo = ordenarPedidos([ana, beto], { columna: "legajo", direccion: "asc" });
    expect(porLegajo.map((p) => p.docente.legajo)).toEqual(["999", "1005"]);
  });

  it("las fechas ordenan cronológicamente, no por su texto dd/mm/aaaa", () => {
    const porInicio = ordenarPedidos([ana, beto], { columna: "inicio", direccion: "asc" });
    expect(porInicio[0]).toBe(beto); // enero antes que marzo
  });

  it("no muta la lista que recibe", () => {
    const original = [ana, beto];
    ordenarPedidos(original, { columna: "docente", direccion: "asc" });
    expect(original).toEqual([ana, beto]);
  });

  it("el ciclo del header es asc → desc → sin orden manual", () => {
    expect(siguienteOrden(null, "docente")).toEqual({ columna: "docente", direccion: "asc" });
    expect(siguienteOrden({ columna: "docente", direccion: "asc" }, "docente")).toEqual({
      columna: "docente",
      direccion: "desc",
    });
    expect(siguienteOrden({ columna: "docente", direccion: "desc" }, "docente")).toBeNull();
  });

  it("cambiar de columna arranca de nuevo en asc", () => {
    expect(siguienteOrden({ columna: "docente", direccion: "desc" }, "legajo")).toEqual({
      columna: "legajo",
      direccion: "asc",
    });
  });
});
