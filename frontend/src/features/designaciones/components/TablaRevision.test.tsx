import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
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

const COMPLETA: FiltrosTablero = {
  vista: "completa",
  tipo: "todos",
  prioridad: "todos",
  nombre: "",
  legajo: "",
};
const MIS_PENDIENTES: FiltrosTablero = {
  vista: "mis-pendientes",
  tipo: "todos",
  prioridad: "todos",
  nombre: "",
  legajo: "",
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

function evento(accion: EventoHistorial["accion"], porNombre = "M. Díaz"): EventoHistorial {
  contador += 1;
  return {
    id: `h${contador}`,
    accion,
    porRol: "Coordinador",
    porNombre,
    etapa: "en_revision_coordinador",
    fecha: "2026-01-01T00:00:00.000Z",
  };
}

/** Aria-label de las filas de pedido (excluye los botones de header de sección). */
function nombresDeFilas() {
  return screen
    .getAllByRole("button")
    .map((b) => b.getAttribute("aria-label"))
    .filter((label): label is string => !!label && label.startsWith("Ver el pedido de"));
}

describe("TablaRevision (secciones por etapa del circuito)", () => {
  it("agrupa los pedidos en las 4 secciones y las ordena Coordinación → Secretaría → Decanato → Finalizados", async () => {
    const user = userEvent.setup();
    const enCoord = pedido("en_revision_coordinador", {
      docente: { dni: "1", nombre: "Coordinación Uno", antiguedad: 3 },
    });
    const enSecre = pedido("en_revision_secretaria", {
      docente: { dni: "2", nombre: "Secretaría Dos", antiguedad: 3 },
    });
    const enDecanato = pedido("en_revision_decanato", {
      docente: { dni: "3", nombre: "Decanato Tres", antiguedad: 3 },
    });
    const aceptado = pedido("en_lote", {
      docente: { dni: "4", nombre: "Aceptado Cuatro", antiguedad: 3 },
    });
    const rechazado = pedido("rechazado", {
      docente: { dni: "5", nombre: "Rechazado Cinco", antiguedad: 3 },
    });

    render(
      <TablaRevision
        pedidos={[rechazado, aceptado, enDecanato, enSecre, enCoord]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.getByText("En Coordinación")).toBeInTheDocument();
    expect(screen.getByText("En Secretaría")).toBeInTheDocument();
    expect(screen.getByText("En Decanato")).toBeInTheDocument();
    expect(screen.getByText("Finalizados")).toBeInTheDocument();
    // Ya no existen secciones separadas "Devueltos"/"Rechazados"/"Aceptados".
    expect(screen.queryByText("Devueltos")).not.toBeInTheDocument();
    expect(screen.queryByText("Rechazados")).not.toBeInTheDocument();
    expect(screen.queryByText("Aceptados")).not.toBeInTheDocument();

    // El Coordinador solo arranca con "En Coordinación" expandida; se expanden las demás a mano.
    await user.click(screen.getByRole("button", { name: "Expandir sección En Secretaría" }));
    await user.click(screen.getByRole("button", { name: "Expandir sección En Decanato" }));
    await user.click(screen.getByRole("button", { name: "Expandir sección Finalizados" }));

    expect(nombresDeFilas()).toEqual([
      "Ver el pedido de Coordinación Uno",
      "Ver el pedido de Secretaría Dos",
      "Ver el pedido de Decanato Tres",
      "Ver el pedido de Aceptado Cuatro",
      "Ver el pedido de Rechazado Cinco",
    ]);
  });

  it("el contador de cada sección muestra 'Total: {n}', no solo el número", () => {
    const enCoord1 = pedido("en_revision_coordinador", {
      docente: { dni: "22", nombre: "Coordinación Veintidós", antiguedad: 3 },
    });
    const enCoord2 = pedido("en_revision_coordinador", {
      docente: { dni: "23", nombre: "Coordinación Veintitrés", antiguedad: 3 },
    });

    render(
      <TablaRevision
        pedidos={[enCoord1, enCoord2]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.getByText("Total: 2")).toBeInTheDocument();
    expect(screen.queryByText("2", { selector: ".adoc-seccion-contador" })).not.toBeInTheDocument();
  });

  it("arranca con la sección del rol del actor expandida y las demás colapsadas", () => {
    const enCoord = pedido("en_revision_coordinador", {
      docente: { dni: "16", nombre: "Coordinación Dieciséis", antiguedad: 3 },
    });
    const enSecre = pedido("en_revision_secretaria", {
      docente: { dni: "17", nombre: "Secretaría Diecisiete", antiguedad: 3 },
    });
    const enDecanato = pedido("en_revision_decanato", {
      docente: { dni: "18", nombre: "Decanato Dieciocho", antiguedad: 3 },
    });

    const { unmount } = render(
      <TablaRevision
        pedidos={[enCoord, enSecre, enDecanato]}
        actor={SECRE}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    // Actor Secretaría: solo "En Secretaría" arranca expandida.
    expect(screen.getByText("Secretaría Diecisiete")).toBeInTheDocument();
    expect(screen.queryByText("Coordinación Dieciséis")).not.toBeInTheDocument();
    expect(screen.queryByText("Decanato Dieciocho")).not.toBeInTheDocument();
    unmount();

    render(
      <TablaRevision
        pedidos={[enCoord, enSecre, enDecanato]}
        actor={DECANO}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    // Actor Decanato: solo "En Decanato" arranca expandida.
    expect(screen.getByText("Decanato Dieciocho")).toBeInTheDocument();
    expect(screen.queryByText("Coordinación Dieciséis")).not.toBeInTheDocument();
    expect(screen.queryByText("Secretaría Diecisiete")).not.toBeInTheDocument();
  });

  it("Administración no tiene sección propia: las 4 arrancan colapsadas", () => {
    const enCoord = pedido("en_revision_coordinador", {
      docente: { dni: "19", nombre: "Coordinación Diecinueve", antiguedad: 3 },
    });

    render(
      <TablaRevision
        pedidos={[enCoord]}
        actor={ADMIN}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.queryByText("Coordinación Diecinueve")).not.toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: /^Expandir sección/ })).toHaveLength(4);
  });

  it("cada sección expandida tiene su propio head de columnas (no uno compartido)", async () => {
    const user = userEvent.setup();
    const enCoord = pedido("en_revision_coordinador", {
      docente: { dni: "20", nombre: "Coordinación Veinte", antiguedad: 3 },
    });
    const enSecre = pedido("en_revision_secretaria", {
      docente: { dni: "21", nombre: "Secretaría Veintiuno", antiguedad: 3 },
    });

    render(
      <TablaRevision
        pedidos={[enCoord, enSecre]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    // Solo "En Coordinación" expandida: un solo head de columnas.
    expect(screen.getAllByText("Docente")).toHaveLength(1);
    expect(screen.getAllByText("Tipo")).toHaveLength(1);

    await user.click(screen.getByRole("button", { name: "Expandir sección En Secretaría" }));

    // Con las 2 expandidas, cada una tiene su propio head.
    expect(screen.getAllByText("Docente")).toHaveLength(2);
    expect(screen.getAllByText("Tipo")).toHaveLength(2);
  });

  it("un pedido devuelto vive en la sección de su etapaRetorno y mantiene el stepper + etapa, con 'Devuelto por' al costado", () => {
    const devuelto = pedido("devuelto", {
      docente: { dni: "6", nombre: "Devuelto Seis", antiguedad: 3 },
      etapaRetorno: "en_revision_decanato",
      propietarioActual: "Secretaría",
      historial: [evento("devolver", "S. Gómez")],
    });

    // Actor Decanato: "En Decanato" (la sección del etapaRetorno) arranca expandida.
    render(
      <TablaRevision
        pedidos={[devuelto]}
        actor={DECANO}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(
      screen.getByText("En Decanato · 3/4 · Devuelto por S. Gómez (Coordinador)"),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Ver el pedido de Devuelto Seis" }),
    ).toBeInTheDocument();
  });

  it("muestra el nombre del docente sin el prefijo 'Prof.'", () => {
    const fila = pedido("en_revision_coordinador", {
      docente: { dni: "7", nombre: "Sin Prefijo Siete", antiguedad: 3 },
    });

    render(
      <TablaRevision pedidos={[fila]} actor={COORD} filtros={COMPLETA} onSeleccionar={vi.fn()} />,
    );

    expect(screen.getByText("Sin Prefijo Siete")).toBeInTheDocument();
    expect(screen.queryByText(/Prof\./)).not.toBeInTheDocument();
  });

  it("la sección del rol del actor arranca expandida; colapsarla la oculta, expandir otra la muestra", async () => {
    const user = userEvent.setup();
    const enCoord = pedido("en_revision_coordinador", {
      docente: { dni: "8", nombre: "Coordinación Ocho", antiguedad: 3 },
    });
    const enSecre = pedido("en_revision_secretaria", {
      docente: { dni: "9", nombre: "Secretaría Nueve", antiguedad: 3 },
    });

    render(
      <TablaRevision
        pedidos={[enCoord, enSecre]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.getByText("Coordinación Ocho")).toBeInTheDocument();
    expect(screen.queryByText("Secretaría Nueve")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Colapsar sección En Coordinación" }));
    expect(screen.queryByText("Coordinación Ocho")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Expandir sección En Secretaría" }));
    expect(screen.getByText("Secretaría Nueve")).toBeInTheDocument();
  });

  it("la columna Prioritario muestra la bandera solo en los pedidos prioritarios", () => {
    const prioritario = pedido("en_revision_coordinador", {
      docente: { dni: "10", nombre: "Urgente Diez", antiguedad: 3 },
      prioritario: true,
    });
    const normal = pedido("en_revision_coordinador", {
      docente: { dni: "11", nombre: "Normal Once", antiguedad: 3 },
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

  it("muestra Legajo, header Tipo y Fecha última actualización por fila", () => {
    const conLegajo = pedido("en_revision_coordinador", {
      docente: { dni: "12", nombre: "Con Legajo Doce", antiguedad: 3, legajo: "1005" },
      historial: [
        {
          id: "h1",
          accion: "crear",
          porRol: "Jefe de Cátedra",
          porNombre: "X",
          etapa: "borrador",
          fecha: "2026-01-05T00:00:00.000Z",
        },
        {
          id: "h2",
          accion: "enviar",
          porRol: "Jefe de Cátedra",
          porNombre: "X",
          etapa: "en_revision_coordinador",
          fecha: "2026-03-10T00:00:00.000Z",
        },
      ],
    });
    const sinLegajo = pedido("en_revision_coordinador", {
      docente: { dni: "13", nombre: "Sin Legajo Trece", antiguedad: 3 },
    });

    render(
      <TablaRevision
        pedidos={[conLegajo, sinLegajo]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={vi.fn()}
      />,
    );

    expect(screen.getByText("Tipo")).toBeInTheDocument();
    expect(screen.queryByText("Novedad")).not.toBeInTheDocument();
    expect(screen.getByText("1005")).toBeInTheDocument();
    expect(screen.getByText("10/03/2026")).toBeInTheDocument();
    // El pedido sin legajo muestra "—" en esa columna.
    expect(screen.getAllByText("—").length).toBeGreaterThan(0);
  });

  it("una sección sin pedidos que cumplan los filtros muestra su estado vacío al expandirla, sin romper el resto", async () => {
    const user = userEvent.setup();
    const ajeno = pedido("en_revision_secretaria", {
      docente: { dni: "14", nombre: "Ajeno Catorce", antiguedad: 3 },
    });

    render(
      <TablaRevision
        pedidos={[ajeno]}
        actor={COORD}
        filtros={MIS_PENDIENTES}
        onSeleccionar={vi.fn()}
      />,
    );

    // "Mis pendientes" para un Coordinador deja "En Coordinación" (la suya, expandida) vacía —
    // el pedido de Secretaría no es su turno.
    expect(screen.getByText("Sin pedidos")).toBeInTheDocument();
    expect(screen.queryByText(/Ajeno Catorce/)).not.toBeInTheDocument();

    // Expandir "En Secretaría" (colapsada por default) también la muestra vacía.
    await user.click(screen.getByRole("button", { name: "Expandir sección En Secretaría" }));
    expect(screen.getAllByText("Sin pedidos")).toHaveLength(2);
  });

  it("navega al hacer click en una fila", async () => {
    const user = userEvent.setup();
    const onSeleccionar = vi.fn();
    const fila = pedido("en_revision_coordinador", {
      docente: { dni: "15", nombre: "Click Quince", antiguedad: 3 },
    });

    render(
      <TablaRevision
        pedidos={[fila]}
        actor={COORD}
        filtros={COMPLETA}
        onSeleccionar={onSeleccionar}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Ver el pedido de Click Quince" }));
    expect(onSeleccionar).toHaveBeenCalledWith(fila);
  });
});
