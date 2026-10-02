import { describe, expect, it } from "vitest";
import type { EstadoPedido, PedidoDesignacion } from "../types";
import {
  aplicarFiltrosMisPedidos,
  aplicarFiltrosYOrdenMisPedidos,
  FILTROS_INICIALES,
  siguienteOrdenMisPedidos,
} from "./filtrosMisPedidos";

let contador = 0;
function pedido(overrides: Partial<PedidoDesignacion> = {}): PedidoDesignacion {
  contador += 1;
  return {
    id: `p${contador}`,
    numero: `N°-2026-${String(contador).padStart(4, "0")}`,
    periodoId: "periodo-1",
    catedra: "Cálculo I",
    carrera: "Ingeniería en Informática",
    docente: { dni: `${contador}`, nombre: "Ana García", legajo: "1005", antiguedad: 3 },
    horas: 6,
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Alta",
    horasExternas: 0,
    horasInvestigacion: 0,
    adjuntos: [],
    estado: "en_revision_coordinador" as EstadoPedido,
    prioritario: false,
    historial: [
      {
        id: `h${contador}`,
        accion: "enviar",
        porRol: "Jefe de Cátedra",
        porNombre: "J. Cátedra",
        etapa: "en_revision_coordinador",
        fecha: "2026-03-10T00:00:00.000Z",
      },
    ],
    ...overrides,
  };
}

describe("filtros de Mis pedidos", () => {
  it("compara texto sin tildes y combina columnas con AND", () => {
    const resultado = aplicarFiltrosMisPedidos(
      [
        pedido(),
        pedido({ catedra: "Física", docente: { dni: "2", nombre: "Beto Pérez", antiguedad: 3 } }),
      ],
      { ...FILTROS_INICIALES, docente: "garcia", catedra: "calculo" },
    );

    expect(resultado).toHaveLength(1);
    expect(resultado[0].docente.nombre).toBe("Ana García");
  });

  it("combina varias opciones de una columna con OR", () => {
    const resultado = aplicarFiltrosMisPedidos(
      [
        pedido({ novedad: "Alta" }),
        pedido({ novedad: "Baja" }),
        pedido({ novedad: "Sin novedad" }),
      ],
      { ...FILTROS_INICIALES, tipo: ["Alta", "Baja"] },
    );

    expect(resultado).toHaveLength(2);
  });

  it("filtra antes de ordenar y conserva el ciclo asc/desc/sin orden", () => {
    const excluido = pedido({ docente: { dni: "3", nombre: "Afuera", antiguedad: 3 } });
    const beto = pedido({ docente: { dni: "2", nombre: "Beto", antiguedad: 3 } });
    const ana = pedido({ docente: { dni: "1", nombre: "Ana", antiguedad: 3 } });

    const resultado = aplicarFiltrosYOrdenMisPedidos(
      [beto, excluido, ana],
      { ...FILTROS_INICIALES, docente: "a" },
      { columna: "docente", direccion: "asc" },
    );

    expect(resultado.map((item) => item.docente.nombre)).toEqual(["Afuera", "Ana"]);
    expect(resultado.slice(0, 1).map((item) => item.docente.nombre)).toEqual(["Afuera"]);
    expect(siguienteOrdenMisPedidos(null, "docente")).toEqual({
      columna: "docente",
      direccion: "asc",
    });
    expect(siguienteOrdenMisPedidos({ columna: "docente", direccion: "asc" }, "docente")).toEqual({
      columna: "docente",
      direccion: "desc",
    });
    expect(
      siguienteOrdenMisPedidos({ columna: "docente", direccion: "desc" }, "docente"),
    ).toBeNull();
  });

  it("ordena fechas por el instante subyacente", () => {
    const marzo = pedido({
      historial: [
        {
          id: "marzo",
          accion: "enviar",
          porRol: "Jefe de Cátedra",
          porNombre: "J. Cátedra",
          etapa: "en_revision_coordinador",
          fecha: "2026-03-10T00:00:00.000Z",
        },
      ],
    });
    const enero = pedido({
      historial: [
        {
          id: "enero",
          accion: "enviar",
          porRol: "Jefe de Cátedra",
          porNombre: "J. Cátedra",
          etapa: "en_revision_coordinador",
          fecha: "2026-01-10T00:00:00.000Z",
        },
      ],
    });

    expect(
      aplicarFiltrosYOrdenMisPedidos([marzo, enero], FILTROS_INICIALES, {
        columna: "enviado",
        direccion: "asc",
      })[0],
    ).toBe(enero);
  });

  it("deja los valores vacíos agrupados al final en ambas direcciones", () => {
    const conLegajo = pedido({
      docente: { dni: "1", nombre: "Con legajo", legajo: "100", antiguedad: 3 },
    });
    const sinLegajo = pedido({ docente: { dni: "2", nombre: "Sin legajo", antiguedad: 3 } });

    expect(
      aplicarFiltrosYOrdenMisPedidos([sinLegajo, conLegajo], FILTROS_INICIALES, {
        columna: "legajo",
        direccion: "asc",
      }).map((item) => item.id),
    ).toEqual([conLegajo.id, sinLegajo.id]);
    expect(
      aplicarFiltrosYOrdenMisPedidos([conLegajo, sinLegajo], FILTROS_INICIALES, {
        columna: "legajo",
        direccion: "desc",
      }).map((item) => item.id),
    ).toEqual([conLegajo.id, sinLegajo.id]);
  });
});
