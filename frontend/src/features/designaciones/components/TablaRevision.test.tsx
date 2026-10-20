import { describe, it, expect, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { TablaRevision } from "./TablaRevision";
import type { FiltrosTablero } from "./filtrosTablero";
import type { ActorContexto, EstadoPedido, PedidoDesignacion, PeriodoDesignacion } from "../types";

const COORD: ActorContexto = {
  rol: "Coordinador",
  nombre: "M. Díaz",
  carrera: "Ingeniería en Informática",
};
const SECRE: ActorContexto = { rol: "Secretaría", nombre: "L. Fernández" };
const DECANO: ActorContexto = { rol: "Decanato", nombre: "R. Sosa" };
const ADMIN: ActorContexto = { rol: "Administración", nombre: "Admin" };
const PERIODO_ACTIVO: PeriodoDesignacion = {
  id: "periodo-1",
  nombre: "Segundo cuatrimestre 2026",
  cargaDesde: "2026-06-01",
  cargaHasta: "2026-07-31",
  impactoDesde: "2026-08-01",
  impactoHasta: "2026-12-31",
  activo: true,
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
  it("muestra Exportar sólo en Finalizados con el período explícito", async () => {
    const user = userEvent.setup();
    const onExportar = vi.fn();
    render(
      <TablaRevision
        pedidos={[pedido("en_lote")]}
        actor={ADMIN}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
        periodoActivo={PERIODO_ACTIVO}
        onExportar={onExportar}
      />,
    );

    expect(screen.queryByRole("button", { name: "Exportar" })).not.toBeInTheDocument();
    await user.click(pestania(/Finalizados/));
    expect(screen.getByText("Período: Segundo cuatrimestre 2026")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Exportar" }));
    expect(onExportar).toHaveBeenCalledOnce();
  });

  it.each([COORD, SECRE, DECANO, ADMIN])(
    "muestra Exportar sólo a roles de revisión habilitados (%s)",
    async (actor) => {
      const user = userEvent.setup();
      render(
        <TablaRevision
          pedidos={[pedido("en_lote")]}
          actor={actor}
          filtros={SIN_FILTROS}
          onSeleccionar={vi.fn()}
          periodoActivo={PERIODO_ACTIVO}
          onExportar={vi.fn()}
        />,
      );

      await user.click(pestania(/Finalizados/));
      if (
        actor.rol === "Secretaría" ||
        actor.rol === "Decanato" ||
        actor.rol === "Administración"
      ) {
        expect(screen.getByRole("button", { name: "Exportar" })).toBeInTheDocument();
      } else {
        expect(screen.queryByRole("button", { name: "Exportar" })).not.toBeInTheDocument();
      }
    },
  );

  it("deja Exportar deshabilitado y explica la falta de período activo", async () => {
    const user = userEvent.setup();
    render(
      <TablaRevision
        pedidos={[pedido("en_lote")]}
        actor={ADMIN}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
        periodoActivo={null}
        onExportar={vi.fn()}
      />,
    );

    await user.click(pestania(/Finalizados/));
    expect(screen.getByRole("button", { name: "Exportar" })).toBeDisabled();
    expect(
      screen.getByText("Configurá un período activo para descargar el lote."),
    ).toBeInTheDocument();
  });

  it("muestra progreso y permite reintentar tras un error", async () => {
    const user = userEvent.setup();
    const onExportar = vi.fn();
    const { rerender } = render(
      <TablaRevision
        pedidos={[pedido("en_lote")]}
        actor={ADMIN}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
        periodoActivo={PERIODO_ACTIVO}
        onExportar={onExportar}
        exportando
      />,
    );

    await user.click(pestania(/Finalizados/));
    expect(screen.getByRole("button", { name: "Exportar" })).toBeDisabled();
    expect(screen.getByRole("status")).toHaveTextContent("Exportando…");

    rerender(
      <TablaRevision
        pedidos={[pedido("en_lote")]}
        actor={ADMIN}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
        periodoActivo={PERIODO_ACTIVO}
        onExportar={onExportar}
        errorExportacion="No se pudo descargar el lote. Reintentá."
      />,
    );
    expect(screen.getByRole("alert")).toHaveTextContent("Reintentá");
    await user.click(screen.getByRole("button", { name: "Exportar" }));
    expect(onExportar).toHaveBeenCalledOnce();
  });

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

    await user.click(screen.getByText(p.docente.nombre));
    expect(onSeleccionar).toHaveBeenCalledWith(p);

    screen.getByText(p.docente.nombre).closest("tr")!.focus();
    await user.keyboard("{Enter}");
    expect(onSeleccionar).toHaveBeenCalledTimes(2);
  });
});
