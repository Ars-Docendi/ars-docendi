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

const MIS_PENDIENTES: FiltrosTablero = {
  vista: "mis-pendientes",
  tipo: "todos",
  prioridad: "todos",
};
const COMPLETA: FiltrosTablero = { vista: "completa", tipo: "todos", prioridad: "todos" };

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

describe("TableroRevision (Kanban relativo al rol)", () => {
  it("ubica cada pedido en su columna del pipeline para el Coordinador", () => {
    const pendiente = pedido("en_revision_coordinador", {
      docente: { dni: "1", nombre: "Pendiente Uno", antiguedad: 3 },
    });
    const siguiente = pedido("en_revision_secretaria", {
      docente: { dni: "2", nombre: "Siguiente Dos", antiguedad: 3 },
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
        pedidos={[pendiente, siguiente, aceptado, rechazado, devuelto]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(columna("Pendientes").getByText(/Pendiente Uno/)).toBeInTheDocument();
    expect(columna("En Secretaría").getByText(/Siguiente Dos/)).toBeInTheDocument();
    expect(columna("Aceptados").getByText(/Aceptado Tres/)).toBeInTheDocument();
    expect(columna("Rechazados").getByText(/Rechazado Cuatro/)).toBeInTheDocument();
    expect(columna("Devueltos").getByText(/Devuelto Cinco/)).toBeInTheDocument();
  });

  it("la vista 'mis-pendientes' oculta los rechazados; la 'completa' los muestra", () => {
    const rechazado = pedido("rechazado", {
      docente: { dni: "9", nombre: "Rechazado Oculto", antiguedad: 3 },
    });

    const { rerender } = render(
      <TableroRevision
        pedidos={[rechazado]}
        actor={COORD}
        filtros={MIS_PENDIENTES}
        onSeleccionar={vi.fn()}
      />,
    );
    expect(screen.queryByText(/Rechazado Oculto/)).not.toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "Rechazados" })).not.toBeInTheDocument();

    rerender(
      <TableroRevision
        pedidos={[rechazado]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );
    expect(screen.getByText(/Rechazado Oculto/)).toBeInTheDocument();
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
        filtros={MIS_PENDIENTES}
        onSeleccionar={onSeleccionar}
      />,
    );

    expect(screen.getByText("Prioritario")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Ver el pedido de Urgente Nueve" }));
    expect(onSeleccionar).toHaveBeenCalledWith(prioritario);
  });
});
