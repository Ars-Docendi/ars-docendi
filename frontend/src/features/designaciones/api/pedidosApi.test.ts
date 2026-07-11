import { describe, it, expect } from "vitest";
import {
  aceptarPedido,
  crearPedido,
  enviarPedido,
  listarMisPedidos,
  listarPedidosPorAmbito,
  obtenerPedido,
} from "./pedidosApi";
import { reiniciarStorePedidos } from "./pedidosStore";
import type { ActorContexto, DatosEditablesPedido } from "../types";

const JC: ActorContexto = {
  rol: "Jefe de Cátedra",
  nombre: "G. Ruiz",
  carrera: "Ingeniería en Informática",
  catedra: "Ingeniería de Software",
};

const COORD: ActorContexto = {
  rol: "Coordinador",
  nombre: "M. Díaz",
  carrera: "Ingeniería en Informática",
};
const SECRE: ActorContexto = { rol: "Secretaría", nombre: "L. Fernández" };

const DATOS_ALTA: DatosEditablesPedido = {
  docente: { dni: "40222333", nombre: "Camila Vega", antiguedad: 0 },
  asignaciones: [{ materia: "Ingeniería de Software", horas: 6 }],
  cargoActual: null,
  dedicacionActual: null,
  novedad: "Alta",
  cargoSolicitado: "Ayudante",
  dedicacionSolicitada: "Categoría 5",
  horasExternas: 0,
  horasInvestigacion: 0,
  adjuntos: [
    { id: "a1", nombre: "cv.pdf", tipo: "cv" },
    { id: "a2", nombre: "frente.jpg", tipo: "dni_frente" },
    { id: "a3", nombre: "dorso.jpg", tipo: "dni_dorso" },
  ],
};

describe("pedidosApi — seam mock", () => {
  it("lista el seed de 'Mis pedidos' acotado a la cátedra del JC", async () => {
    const lista = await listarMisPedidos(JC);
    expect(lista.length).toBeGreaterThan(0);
    expect(lista.every((p) => p.catedra === "Ingeniería de Software")).toBe(true);
  });

  it("crear agrega un pedido en borrador con un evento 'crear'", async () => {
    const creado = await crearPedido(DATOS_ALTA, JC);
    expect(creado.estado).toBe("borrador");
    expect(creado.historial.at(-1)?.accion).toBe("crear");

    const lista = await listarMisPedidos(JC);
    expect(lista.some((p) => p.id === creado.id)).toBe(true);
  });

  it("el estado persiste entre recargas (reset + re-hidratación del store)", async () => {
    const creado = await crearPedido(DATOS_ALTA, JC);
    await enviarPedido(creado.id, JC);

    // Simula una recarga: el singleton se resetea y re-hidrata desde localStorage.
    reiniciarStorePedidos();

    const recuperado = await obtenerPedido(creado.id);
    expect(recuperado.estado).toBe("en_revision_coordinador");
  });
});

describe("pedidosApi — seam de revisión (SCRUM-8)", () => {
  it("listarPedidosPorAmbito acota a la carrera del Coordinador [BR-009]", async () => {
    const lista = await listarPedidosPorAmbito(COORD);
    expect(lista.length).toBeGreaterThan(0);
    expect(lista.every((p) => p.carrera === "Ingeniería en Informática")).toBe(true);
    // El pedido sembrado de Ingeniería Industrial NO debe aparecer.
    expect(lista.some((p) => p.carrera === "Ingeniería Industrial")).toBe(false);
  });

  it("listarPedidosPorAmbito es depto-wide para Secretaría", async () => {
    const lista = await listarPedidosPorAmbito(SECRE);
    const carreras = new Set(lista.map((p) => p.carrera));
    expect(carreras.has("Ingeniería en Informática")).toBe(true);
    expect(carreras.has("Ingeniería Industrial")).toBe(true);
  });

  it("una aceptación persiste el avance de etapa entre recargas", async () => {
    const enCoordinador = (await listarPedidosPorAmbito(COORD)).find(
      (p) => p.estado === "en_revision_coordinador",
    );
    expect(enCoordinador).toBeDefined();

    await aceptarPedido(enCoordinador!.id, COORD);
    reiniciarStorePedidos();

    const recuperado = await obtenerPedido(enCoordinador!.id);
    expect(recuperado.estado).toBe("en_revision_secretaria");
  });
});
