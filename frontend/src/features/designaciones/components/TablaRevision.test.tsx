import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { TablaRevision } from "./TablaRevision";
import type { FiltrosTablero } from "./filtrosTablero";
import type { ActorContexto, EstadoPedido, PedidoDesignacion } from "../types";

const COORD: ActorContexto = {
  rol: "Coordinador",
  nombre: "M. Díaz",
  carrera: "Ingeniería en Informática",
};

const COMPLETA: FiltrosTablero = { vista: "completa", tipo: "todos", prioridad: "todos" };
const MIS_PENDIENTES: FiltrosTablero = {
  vista: "mis-pendientes",
  tipo: "todos",
  prioridad: "todos",
};

let contador = 0;
function pedido(
  estado: EstadoPedido,
  overrides: Partial<PedidoDesignacion> = {},
): PedidoDesignacion {
  contador += 1;
  return {
    id: `p${contador}`,
    periodoId: "1",
    catedra: "Cátedra X",
    carrera: "Ingeniería en Informática",
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

describe("TablaRevision (vista Tabla — opción D)", () => {
  it("aplana los pedidos en filas ordenadas por estado con la columna Estado combinada", () => {
    const enRevision = pedido("en_revision_coordinador", {
      docente: { dni: "1", nombre: "Revisión Uno", antiguedad: 3 },
    });
    const aceptado = pedido("en_lote", {
      docente: { dni: "2", nombre: "Aceptado Dos", antiguedad: 3 },
    });
    const devuelto = pedido("devuelto", {
      docente: { dni: "3", nombre: "Devuelto Tres", antiguedad: 3 },
    });
    const rechazado = pedido("rechazado", {
      docente: { dni: "4", nombre: "Rechazado Cuatro", antiguedad: 3 },
    });

    render(
      <TablaRevision
        pedidos={[rechazado, devuelto, aceptado, enRevision]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    // La columna Estado combina estado + avance.
    expect(screen.getByText("En Coordinación · 1/4")).toBeInTheDocument();
    expect(screen.getByText("Aceptado")).toBeInTheDocument();
    expect(screen.getByText("Devuelto")).toBeInTheDocument();
    expect(screen.getByText("Rechazado")).toBeInTheDocument();

    // Orden de filas: En revisión → Aceptados → Devueltos → Rechazados.
    const filas = screen.getAllByRole("button").map((b) => b.getAttribute("aria-label"));
    expect(filas).toEqual([
      "Ver el pedido de Revisión Uno",
      "Ver el pedido de Aceptado Dos",
      "Ver el pedido de Devuelto Tres",
      "Ver el pedido de Rechazado Cuatro",
    ]);
  });

  it("la columna Prioritario muestra la bandera solo en los pedidos prioritarios", () => {
    const prioritario = pedido("en_revision_coordinador", {
      docente: { dni: "9", nombre: "Urgente Nueve", antiguedad: 3 },
      prioritario: true,
    });
    const normal = pedido("en_revision_coordinador", {
      docente: { dni: "8", nombre: "Normal Ocho", antiguedad: 3 },
    });

    render(
      <TablaRevision
        pedidos={[prioritario, normal]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.getAllByLabelText("Prioritario")).toHaveLength(1);
  });

  it("muestra el estado vacío cuando los filtros no dejan filas", () => {
    const ajeno = pedido("en_revision_secretaria", {
      docente: { dni: "5", nombre: "Ajeno Cinco", antiguedad: 3 },
    });

    render(
      <TablaRevision
        pedidos={[ajeno]}
        actor={COORD}
        filtros={MIS_PENDIENTES}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.getByText("Sin pedidos")).toBeInTheDocument();
    expect(screen.queryByText(/Ajeno Cinco/)).not.toBeInTheDocument();
  });

  it("navega al hacer click en una fila", async () => {
    const user = userEvent.setup();
    const onSeleccionar = vi.fn();
    const fila = pedido("en_revision_coordinador", {
      docente: { dni: "7", nombre: "Click Siete", antiguedad: 3 },
    });

    render(
      <TablaRevision
        pedidos={[fila]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={onSeleccionar}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Ver el pedido de Click Siete" }));
    expect(onSeleccionar).toHaveBeenCalledWith(fila);
  });
});
