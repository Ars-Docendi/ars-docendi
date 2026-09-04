import { describe, it, expect, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { TablaRevision } from "./TablaRevision";
import type { FiltrosTablero } from "./filtrosTablero";
import type { ActorContexto, EstadoPedido, EventoHistorial, PedidoDesignacion } from "../types";

const COORD: ActorContexto = {
  rol: "Coordinador",
  nombre: "M. Díaz",
  carrera: "Ingeniería en Informática",
};
const SECRE: ActorContexto = { rol: "Secretaría", nombre: "L. Fernández" };
const DECANO: ActorContexto = { rol: "Decanato", nombre: "R. Sosa" };
const ADMIN: ActorContexto = { rol: "Administración", nombre: "Admin" };

const SIN_FILTROS: FiltrosTablero = {
  tipo: "todos",
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
    asignaciones: [{ materia: "Materia X", horas: 6 }],
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

function evento(accion: EventoHistorial["accion"], fecha = "2026-01-01T00:00:00.000Z") {
  contador += 1;
  return {
    id: `h${contador}`,
    accion,
    porRol: "Coordinador" as const,
    porNombre: "M. Díaz",
    etapa: "en_revision_coordinador" as EstadoPedido,
    fecha,
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
  it("usa una sola tabla del design system, con un único head de columnas", () => {
    render(
      <TablaRevision
        pedidos={[pedido("en_revision_coordinador")]}
        actor={COORD}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    // Antes eran 4 mini-tablas, cada una con su head repetido.
    expect(screen.getAllByRole("table")).toHaveLength(1);
    for (const columna of [
      "Docente",
      "Legajo",
      "Tipo",
      "Inicio",
      "Últ. actualización",
      "Estado",
      "Acciones",
    ]) {
      expect(screen.getByRole("columnheader", { name: columna })).toBeInTheDocument();
    }
  });

  it("la columna Área existe solo en Todos: en una pestaña de área sería constante", async () => {
    const user = userEvent.setup();
    render(
      <TablaRevision
        pedidos={[pedido("en_revision_coordinador")]}
        actor={COORD}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    // Abre en "En Coordinación": el área la dice la pestaña, no hace falta la columna.
    expect(screen.queryByRole("columnheader", { name: "Área" })).not.toBeInTheDocument();

    await user.click(pestania(/Todos/));
    expect(screen.getByRole("columnheader", { name: "Área" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "Coordinación" })).toBeInTheDocument();
  });

  it("no tiene columnas Carrera ni Asignatura: una designación puede tener más de una de cada una", () => {
    render(
      <TablaRevision
        pedidos={[pedido("en_revision_coordinador", { carrera: "Ingeniería Industrial" })]}
        actor={COORD}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.queryByRole("columnheader", { name: "Carrera" })).not.toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "Asignatura" })).not.toBeInTheDocument();
  });

  it("muestra las 6 pestañas con su contador, y abre en el área propia del actor", () => {
    render(
      <TablaRevision
        pedidos={[
          pedido("en_revision_coordinador", {
            docente: { dni: "1", nombre: "En Coordinación", antiguedad: 3 },
          }),
          pedido("en_revision_secretaria", {
            docente: { dni: "2", nombre: "En Secretaría", antiguedad: 3 },
          }),
        ]}
        actor={COORD}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.getAllByRole("tab")).toHaveLength(6);
    expect(pestania(/Todos/)).toBeInTheDocument();
    // La Cátedra no revisa, pero retiene los pedidos que Coordinación le devolvió.
    expect(pestania(/En Cátedra/)).toBeInTheDocument();
    expect(pestania(/En Coordinación/)).toHaveAttribute("aria-selected", "true");
    expect(filasVisibles()).toEqual(["En Coordinación"]);
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

  it("Administración no tiene etapa propia: abre en Todos", () => {
    render(
      <TablaRevision
        pedidos={[pedido("en_revision_decanato")]}
        actor={ADMIN}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(pestania(/Todos/)).toHaveAttribute("aria-selected", "true");
  });

  it("Todos muestra el ámbito sin agrupar, incluidos los cancelados que no caen en ninguna etapa", async () => {
    const user = userEvent.setup();

    render(
      <TablaRevision
        pedidos={[
          pedido("en_revision_coordinador", {
            docente: { dni: "1", nombre: "En Circuito", antiguedad: 3 },
          }),
          pedido("cancelado", { docente: { dni: "2", nombre: "Cancelado Dos", antiguedad: 3 } }),
          pedido("borrador", { docente: { dni: "3", nombre: "Borrador Tres", antiguedad: 3 } }),
        ]}
        actor={COORD}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    await user.click(pestania(/Todos/));
    const filas = filasVisibles();
    expect(filas).toContain("En Circuito");
    expect(filas).toContain("Cancelado Dos");
    // Un borrador todavía no entró al circuito: no es asunto del revisor.
    expect(filas).not.toContain("Borrador Tres");
  });

  it("un devuelto vive en la pestaña del área que lo tiene, no en la que lo devolvió", async () => {
    const user = userEvent.setup();
    // Decanato lo devolvió a Secretaría [BR-014]: volverá a Decanato, pero hoy lo
    // tiene Secretaría, así que es ahí donde se lo ve.
    const devuelto = pedido("devuelto", {
      docente: { dni: "6", nombre: "Devuelto Seis", antiguedad: 3 },
      etapaRetorno: "en_revision_decanato",
      propietarioActual: "Secretaría",
      historial: [evento("devolver")],
    });

    render(
      <TablaRevision
        pedidos={[devuelto]}
        actor={DECANO}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.queryByRole("tab", { name: /Devueltos/ })).not.toBeInTheDocument();
    // Decanato abre en su pestaña y NO lo ve: ya no lo tiene.
    expect(pestania(/En Decanato/)).toHaveAttribute("aria-selected", "true");
    expect(screen.queryByText("Devuelto Seis")).not.toBeInTheDocument();

    await user.click(pestania(/En Secretaría/));
    expect(filasVisibles()).toEqual(["Devuelto Seis"]);
    // El badge de Estado nunca lleva el área: eso es la columna Área, que solo aparece
    // en "Todos".
    expect(screen.getByText("Devuelto")).toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "Área" })).not.toBeInTheDocument();

    await user.click(pestania(/Todos/));
    expect(screen.getByRole("cell", { name: "Secretaría" })).toBeInTheDocument();
  });

  it("un devuelto a la Cátedra tiene su propia pestaña: el Coordinador ya no lo tiene", async () => {
    const user = userEvent.setup();
    render(
      <TablaRevision
        pedidos={[
          pedido("devuelto", {
            docente: { dni: "7", nombre: "Devuelto A Cátedra", antiguedad: 3 },
            etapaRetorno: "en_revision_coordinador",
            propietarioActual: "Jefe de Cátedra",
            historial: [evento("devolver")],
          }),
        ]}
        actor={COORD}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    // Antes aparecía bajo "En Coordinación", como si el Coordinador lo tuviera.
    expect(screen.queryByText("Devuelto A Cátedra")).not.toBeInTheDocument();

    await user.click(pestania(/En Cátedra/));
    expect(filasVisibles()).toEqual(["Devuelto A Cátedra"]);
    expect(screen.getByText("Devuelto")).toBeInTheDocument();
  });

  it("la prioridad es un badge más de la celda Estado, sin fondo de fila", () => {
    render(
      <TablaRevision
        pedidos={[
          pedido("en_revision_coordinador", {
            docente: { dni: "10", nombre: "Urgente Diez", antiguedad: 3 },
            prioritario: true,
          }),
          pedido("en_revision_coordinador", {
            docente: { dni: "11", nombre: "Normal Once", antiguedad: 3 },
          }),
        ]}
        actor={COORD}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.getAllByText("Prioritario")).toHaveLength(1);
  });

  it("prioritario y devuelto a la vez muestra los dos badges: ninguno tapa al otro", () => {
    render(
      <TablaRevision
        pedidos={[
          pedido("devuelto", {
            docente: { dni: "25", nombre: "Urgente Devuelto", antiguedad: 3 },
            prioritario: true,
            etapaRetorno: "en_revision_decanato",
            propietarioActual: "Secretaría",
            historial: [evento("devolver")],
          }),
        ]}
        actor={SECRE}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.getByText("Prioritario")).toBeInTheDocument();
    expect(screen.getByText("Devuelto")).toBeInTheDocument();
  });

  it("Inicio es el envío a revisión, no la creación del borrador", () => {
    render(
      <TablaRevision
        pedidos={[
          pedido("en_revision_coordinador", {
            docente: { dni: "12", nombre: "Con Fechas", antiguedad: 3, legajo: "1005" },
            historial: [
              evento("crear", "2026-01-05T00:00:00.000Z"),
              evento("enviar", "2026-03-10T00:00:00.000Z"),
            ],
          }),
        ]}
        actor={COORD}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.getByText("1005")).toBeInTheDocument();
    expect(screen.queryByText("05/01/2026")).not.toBeInTheDocument();
    // El envío es además el último evento: misma fecha en Inicio y Últ. actualización.
    expect(screen.getAllByText("10/03/2026")).toHaveLength(2);
  });

  it("el botón Ver de la fila navega al detalle del pedido", async () => {
    const user = userEvent.setup();
    const alSeleccionar = vi.fn();
    const fila = pedido("en_revision_coordinador", {
      docente: { dni: "3", nombre: "Clickeable Tres", antiguedad: 3 },
    });

    render(
      <TablaRevision
        pedidos={[fila]}
        actor={COORD}
        filtros={SIN_FILTROS}
        onSeleccionar={alSeleccionar}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Ver el pedido de Clickeable Tres" }));
    expect(alSeleccionar).toHaveBeenCalledWith(fila);
  });

  it("una pestaña sin pedidos que cumplan los filtros muestra su estado vacío", async () => {
    const user = userEvent.setup();
    render(
      <TablaRevision
        pedidos={[pedido("en_revision_coordinador")]}
        actor={COORD}
        filtros={SIN_FILTROS}
        onSeleccionar={vi.fn()}
      />,
    );

    await user.click(pestania(/Finalizados/));
    expect(screen.getByText("Sin pedidos que cumplan los filtros.")).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });

  it("los filtros acotan las filas y también los contadores de las pestañas", () => {
    render(
      <TablaRevision
        pedidos={[
          pedido("en_revision_coordinador", {
            docente: { dni: "20", nombre: "Buscado Veinte", antiguedad: 3 },
          }),
          pedido("en_revision_coordinador", {
            docente: { dni: "21", nombre: "Otro Veintiuno", antiguedad: 3 },
          }),
        ]}
        actor={COORD}
        filtros={{ ...SIN_FILTROS, nombre: "buscado" }}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(filasVisibles()).toEqual(["Buscado Veinte"]);
    expect(pestania(/En Coordinación/).textContent).toContain("1");
  });
});
