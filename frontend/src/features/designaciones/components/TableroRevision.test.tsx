import { describe, it, expect, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { TableroRevision } from "./TableroRevision";
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
    catedra: "Ingeniería de Software",
    carrera: "Ingeniería en Informática",
    docente: { dni: "30111222", nombre: `Docente ${contador}`, antiguedad: 5 },
    materiaAsociada: "Ingeniería de Software",
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    novedad: "Sin novedad",
    haceHorasOtroDepto: false,
    adjuntos: [],
    estado,
    prioritario: false,
    historial: [],
    ...overrides,
  };
}

function columna(titulo: string) {
  return within(screen.getByRole("region", { name: titulo }));
}

describe("TableroRevision (modelo D — por estado de avance)", () => {
  it("agrupa por avance: En revisión (toda la cadena) / Aceptados / Devueltos / Rechazados", () => {
    const enCoord = pedido("en_revision_coordinador", {
      docente: { dni: "1", nombre: "Coord Uno", antiguedad: 3 },
    });
    const enSec = pedido("en_revision_secretaria", {
      docente: { dni: "2", nombre: "Secre Dos", antiguedad: 3 },
    });
    const aceptado = pedido("en_lote", {
      docente: { dni: "3", nombre: "Aceptado Tres", antiguedad: 3 },
    });
    const rechazado = pedido("rechazado", {
      docente: { dni: "4", nombre: "Rechazado Cuatro", antiguedad: 3 },
    });
    const devuelto = pedido("devuelto", {
      docente: { dni: "5", nombre: "Devuelto Cinco", antiguedad: 3 },
      propietarioActual: "Jefe de Cátedra",
      etapaRetorno: "en_revision_coordinador",
    });

    render(
      <TableroRevision
        pedidos={[enCoord, enSec, aceptado, rechazado, devuelto]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    // Coordinación y Secretaría conviven en la ÚNICA columna "En revisión".
    expect(columna("En revisión").getByText(/Coord Uno/)).toBeInTheDocument();
    expect(columna("En revisión").getByText(/Secre Dos/)).toBeInTheDocument();
    expect(columna("Aceptados").getByText(/Aceptado Tres/)).toBeInTheDocument();
    expect(columna("Devueltos").getByText(/Devuelto Cinco/)).toBeInTheDocument();
    expect(columna("Rechazados").getByText(/Rechazado Cuatro/)).toBeInTheDocument();
  });

  it("cada card en revisión declara su etapa + avance x/4; 'Tu turno' solo en la etapa del actor", () => {
    const enCoord = pedido("en_revision_coordinador", {
      docente: { dni: "1", nombre: "Coord Uno", antiguedad: 3 },
    });
    const enSec = pedido("en_revision_secretaria", {
      docente: { dni: "2", nombre: "Secre Dos", antiguedad: 3 },
    });

    render(
      <TableroRevision
        pedidos={[enCoord, enSec]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.getByText("En Coordinación · 1/4")).toBeInTheDocument();
    expect(screen.getByText("En Secretaría · 2/4")).toBeInTheDocument();
    // El Coordinador está en turno sobre Coordinación, no sobre Secretaría.
    expect(screen.getAllByText("Tu turno")).toHaveLength(1);
  });

  it("la vista 'mis-pendientes' deja solo los pedidos en turno del actor; 'completa' muestra todo", () => {
    const mio = pedido("en_revision_coordinador", {
      docente: { dni: "1", nombre: "Mío Uno", antiguedad: 3 },
    });
    const ajeno = pedido("en_revision_secretaria", {
      docente: { dni: "2", nombre: "Ajeno Dos", antiguedad: 3 },
    });

    const { rerender } = render(
      <TableroRevision
        pedidos={[mio, ajeno]}
        actor={COORD}
        filtros={MIS_PENDIENTES}
        onSeleccionar={vi.fn()}
      />,
    );
    expect(screen.getByText(/Mío Uno/)).toBeInTheDocument();
    expect(screen.queryByText(/Ajeno Dos/)).not.toBeInTheDocument();

    rerender(
      <TableroRevision
        pedidos={[mio, ajeno]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );
    expect(screen.getByText(/Ajeno Dos/)).toBeInTheDocument();
  });

  it("muestra el flag de prioritario y navega al hacer click en la card", async () => {
    const user = userEvent.setup();
    const onSeleccionar = vi.fn();
    const prioritario = pedido("en_revision_coordinador", {
      docente: { dni: "7", nombre: "Urgente Nueve", antiguedad: 3 },
      prioritario: true,
    });

    render(
      <TableroRevision
        pedidos={[prioritario]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={onSeleccionar}
      />,
    );

    expect(screen.getByText("Prioritario")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Ver el pedido de Urgente Nueve" }));
    expect(onSeleccionar).toHaveBeenCalledWith(prioritario);
  });
});
