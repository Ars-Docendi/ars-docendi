import { describe, it, expect, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { TableroRevision } from "./TableroRevision";
import type { ActorContexto, EstadoPedido, PedidoDesignacion } from "../types";

const COORD: ActorContexto = {
  rol: "Coordinador",
  nombre: "M. Díaz",
  carrera: "Ingeniería en Informática",
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

describe("TableroRevision (Kanban)", () => {
  it("ubica cada pedido en la columna correcta para el Coordinador", () => {
    const pendiente = pedido("en_revision_coordinador", {
      docente: { dni: "1", nombre: "Pendiente Uno", antiguedad: 3 },
    });
    const aprobado = pedido("en_lote", {
      docente: { dni: "2", nombre: "Aprobado Dos", antiguedad: 3 },
    });
    const rechazado = pedido("rechazado", {
      docente: { dni: "3", nombre: "Rechazado Tres", antiguedad: 3 },
    });
    const devuelto = pedido("devuelto", {
      docente: { dni: "4", nombre: "Devuelto Cuatro", antiguedad: 3 },
      propietarioActual: "Jefe de Cátedra",
      etapaRetorno: "en_revision_coordinador",
    });
    // En etapa de Secretaría: no es la etapa del Coordinador → no se muestra.
    const fueraDeEtapa = pedido("en_revision_secretaria", {
      docente: { dni: "5", nombre: "Otra Etapa", antiguedad: 3 },
    });

    render(
      <TableroRevision
        pedidos={[pendiente, aprobado, rechazado, devuelto, fueraDeEtapa]}
        actor={COORD}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(columna("Pendiente (mi etapa)").getByText("Pendiente Uno")).toBeInTheDocument();
    expect(columna("Aprobado").getByText("Aprobado Dos")).toBeInTheDocument();
    expect(columna("Rechazado").getByText("Rechazado Tres")).toBeInTheDocument();
    expect(columna("Devuelto").getByText("Devuelto Cuatro")).toBeInTheDocument();
    // El pedido en etapa de Secretaría no aparece en el tablero del Coordinador.
    expect(screen.queryByText("Otra Etapa")).not.toBeInTheDocument();
  });

  it("muestra el badge de prioritario y navega al hacer click en la card", async () => {
    const user = userEvent.setup();
    const onSeleccionar = vi.fn();
    const prioritario = pedido("en_revision_coordinador", {
      docente: { dni: "9", nombre: "Urgente Nueve", antiguedad: 3 },
      prioritario: true,
    });

    render(<TableroRevision pedidos={[prioritario]} actor={COORD} onSeleccionar={onSeleccionar} />);

    expect(screen.getByText("Prioritario")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Ver el pedido de Urgente Nueve" }));
    expect(onSeleccionar).toHaveBeenCalledWith(prioritario);
  });
});
