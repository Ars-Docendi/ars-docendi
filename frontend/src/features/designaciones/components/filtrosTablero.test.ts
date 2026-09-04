import { describe, it, expect } from "vitest";
import {
  ABREVIATURA_CARRERA,
  CARRERAS,
  FILTROS_INICIALES,
  aplicarFiltros,
  type FiltrosTablero,
} from "./filtrosTablero";
import type { EstadoPedido, PedidoDesignacion } from "../types";

let contador = 0;
function pedido(overrides: Partial<PedidoDesignacion> = {}): PedidoDesignacion {
  contador += 1;
  return {
    id: `p${contador}`,
    periodoId: "1",
    catedra: "Cátedra X",
    carrera: "Ingeniería en Informática",
    docente: { dni: `${contador}`, nombre: `Docente ${contador}`, antiguedad: 3 },
    horas: 6,
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Cambio de cargo o dedicación",
    horasExternas: 0,
    horasInvestigacion: 0,
    esAgenteExterno: false,
    adjuntos: [],
    estado: "en_revision_coordinador" as EstadoPedido,
    prioritario: false,
    historial: [],
    ...overrides,
  };
}

describe("aplicarFiltros — filtro Carrera", () => {
  it("acota los pedidos a la carrera exacta seleccionada", () => {
    const informatica = pedido({ carrera: "Ingeniería en Informática" });
    const industrial = pedido({ carrera: "Ingeniería Industrial" });

    const filtros: FiltrosTablero = { ...FILTROS_INICIALES, carrera: "Ingeniería Industrial" };
    const resultado = aplicarFiltros([informatica, industrial], filtros);

    expect(resultado).toEqual([industrial]);
  });

  it("con 'todos' no filtra por carrera", () => {
    const informatica = pedido({ carrera: "Ingeniería en Informática" });
    const industrial = pedido({ carrera: "Ingeniería Industrial" });

    const resultado = aplicarFiltros([informatica, industrial], FILTROS_INICIALES);

    expect(resultado).toHaveLength(2);
  });

  it("se combina por AND con el filtro Tipo", () => {
    const alta = pedido({ carrera: "Ingeniería Civil", novedad: "Alta" });
    const cambio = pedido({ carrera: "Ingeniería Civil", novedad: "Cambio de cargo o dedicación" });
    const otraCarrera = pedido({ carrera: "Ingeniería Mecánica", novedad: "Alta" });

    const filtros: FiltrosTablero = {
      ...FILTROS_INICIALES,
      carrera: "Ingeniería Civil",
      tipo: "Alta",
    };
    const resultado = aplicarFiltros([alta, cambio, otraCarrera], filtros);

    expect(resultado).toEqual([alta]);
  });
});

describe("ABREVIATURA_CARRERA", () => {
  it("tiene una entrada abreviada para cada carrera del catálogo", () => {
    for (const carrera of CARRERAS) {
      expect(ABREVIATURA_CARRERA[carrera]).toBeTruthy();
    }
  });

  it("abrevia cada carrera al nombre esperado", () => {
    expect(ABREVIATURA_CARRERA["Ingeniería en Informática"]).toBe("Informática");
    expect(ABREVIATURA_CARRERA["Ingeniería Industrial"]).toBe("Industrial");
    expect(ABREVIATURA_CARRERA["Ingeniería Civil"]).toBe("Civil");
    expect(ABREVIATURA_CARRERA["Ingeniería Mecánica"]).toBe("Mecánica");
    expect(ABREVIATURA_CARRERA["Ingeniería Electrónica"]).toBe("Electrónica");
  });
});

describe("aplicarFiltros — período de designación", () => {
  const delActivo = pedido({
    docente: { dni: "a", nombre: "Del Activo", antiguedad: 3 },
    periodoId: "1",
  });
  const delAnterior = pedido({
    docente: { dni: "b", nombre: "Del Anterior", antiguedad: 3 },
    periodoId: "3",
  });

  function nombres(periodo: string) {
    return aplicarFiltros([delActivo, delAnterior], { ...FILTROS_INICIALES, periodo }).map(
      (p) => p.docente.nombre,
    );
  }

  it("acota por el período del pedido", () => {
    // El período es una entidad del dominio, ya creada y con nombre: no hay que
    // adivinar fechas de corte como con un rango libre.
    expect(nombres("1")).toEqual(["Del Activo"]);
    expect(nombres("3")).toEqual(["Del Anterior"]);
  });

  it("'todos' no acota", () => {
    expect(nombres("todos")).toEqual(["Del Activo", "Del Anterior"]);
  });
});

describe("aplicarFiltros — días sin movimiento", () => {
  function movidoHaceDias(dias: number, nombre: string): PedidoDesignacion {
    const fecha = new Date(Date.now() - dias * 24 * 60 * 60 * 1000).toISOString();
    return pedido({
      docente: { dni: nombre, nombre, antiguedad: 3 },
      historial: [
        {
          id: `e-${nombre}`,
          accion: "aceptar",
          porRol: "Coordinador",
          porNombre: "M. Díaz",
          etapa: "en_revision_secretaria",
          fecha,
        },
      ],
    });
  }

  const recien = movidoHaceDias(2, "Recién");
  const hace10 = movidoHaceDias(10, "Hace10");
  const hace40 = movidoHaceDias(40, "Hace40");

  function nombres(sinMovimiento: FiltrosTablero["sinMovimiento"]) {
    return aplicarFiltros([recien, hace10, hace40], { ...FILTROS_INICIALES, sinMovimiento }).map(
      (p) => p.docente.nombre,
    );
  }

  it("deja solo los que superan el umbral de días quietos", () => {
    expect(nombres("7")).toEqual(["Hace10", "Hace40"]);
    expect(nombres("15")).toEqual(["Hace40"]);
    expect(nombres("30")).toEqual(["Hace40"]);
  });

  it("'todos' no acota", () => {
    expect(nombres("todos")).toEqual(["Recién", "Hace10", "Hace40"]);
  });
});
