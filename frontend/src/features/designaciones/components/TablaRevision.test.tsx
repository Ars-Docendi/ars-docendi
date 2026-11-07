import { describe, it, expect, vi } from "vitest";
import { render as renderBase, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import userEvent from "@testing-library/user-event";
import { TablaRevision } from "./TablaRevision";
import type { FiltrosTablero } from "./filtrosTablero";
import type { ActorContexto, EstadoPedido, PedidoDesignacion } from "../types";

/** Las filas llevan enlaces del router: toda renderización necesita uno. */
function render(ui: React.ReactElement) {
  return renderBase(ui, { wrapper: MemoryRouter });
}

const COORD: ActorContexto = {
  rol: "Coordinador",
  nombre: "M. Díaz",
  carrera: "Ingeniería en Informática",
};
const SIN_FILTROS: FiltrosTablero = {
  tipo: "todos",
  estado: "todos",
  prioridad: "todos",
  carrera: "todos",
  nombre: "",
  legajo: "",
  periodo: "todos",
  sinMovimiento: "todos",
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

/** Nombres de docente de las filas visibles, en orden (sin las iniciales del avatar). */
function filasVisibles() {
  const cuerpo = screen.getAllByRole("rowgroup")[1];
  return within(cuerpo)
    .getAllByRole("row")
    .map((fila) => fila.querySelector(".adoc-tabla-nombre")?.textContent);
}

function pestania(nombre: string | RegExp) {
  return screen.getByRole("tab", { name: nombre });
}

describe("TablaRevision (una tabla + pestañas por etapa)", () => {
  it("cambiar de pestaña cambia las filas de la misma tabla", async () => {
    const user = userEvent.setup();
    render(
      <TablaRevision
        pedidos={[
          pedido("en_revision_coordinador", {
            docente: { dni: "1", nombre: "Coordinación Uno", antiguedad: 3 },
          }),
          pedido("en_revision_secretaria", {
            docente: { dni: "2", nombre: "Secretaría Dos", antiguedad: 3 },
          }),
        ]}
        actor={COORD}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(filasVisibles()).toEqual(["Coordinación Uno"]);
    await user.click(pestania(/En Secretaría/));
    expect(filasVisibles()).toEqual(["Secretaría Dos"]);
  });

  it("una pestaña sin pedidos que cumplan los filtros muestra su estado vacío", async () => {
    const user = userEvent.setup();
    render(
      <TablaRevision
        pedidos={[pedido("en_revision_coordinador")]}
        actor={COORD}
        filtros={{ ...SIN_FILTROS, estado: "cancelado" }}
        onSeleccionar={vi.fn()}
      />,
    );

    await user.click(pestania(/Finalizados/));
    expect(screen.getByText("Sin pedidos que cumplan los filtros.")).toBeInTheDocument();
    expect(screen.getByRole("table")).toBeInTheDocument();
  });
});

describe("filtros por encabezado de Revisión", () => {
  it("acota filas y contadores antes de cambiar de pestaña", async () => {
    const user = userEvent.setup();
    render(
      <TablaRevision
        pedidos={[
          pedido("en_revision_coordinador", {
            docente: { dni: "a", nombre: "Ana García", antiguedad: 3 },
          }),
          pedido("en_revision_coordinador", {
            docente: { dni: "b", nombre: "Beto Pérez", antiguedad: 3 },
          }),
        ]}
        actor={COORD}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Filtrar Docente" }));
    await user.type(screen.getByRole("textbox", { name: "Buscar Docente" }), "garcia");

    expect(filasVisibles()).toEqual(["Ana García"]);
    expect(screen.getByRole("tab", { name: "Todos1" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /Docente/ })).toHaveAttribute(
      "aria-sort",
      "none",
    );
  });

  it("la fila abre el detalle con click o Enter, sin botón Ver ni columna Acciones", async () => {
    const user = userEvent.setup();
    const onSeleccionar = vi.fn();
    const p = pedido("en_revision_coordinador");
    render(
      <TablaRevision
        pedidos={[p]}
        actor={COORD}
        filtros={SIN_FILTROS}
        onSeleccionar={onSeleccionar}
      />,
    );

    expect(screen.queryByRole("button", { name: /Ver/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "Acciones" })).not.toBeInTheDocument();

    // Un click fuera del enlace de la celda principal ejecuta la acción de la fila.
    await user.click(screen.getAllByRole("cell")[1]);
    expect(onSeleccionar).toHaveBeenCalledWith(p);

    screen.getByText(p.docente.nombre).closest("tr")!.focus();
    await user.keyboard("{Enter}");
    expect(onSeleccionar).toHaveBeenCalledTimes(2);
  });
});
