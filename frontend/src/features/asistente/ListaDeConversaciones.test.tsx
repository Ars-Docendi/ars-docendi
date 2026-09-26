import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { PanelDePrueba } from "./test/PanelDePrueba";
import * as api from "./api/asistenteApi";
import * as historialApi from "./api/historialApi";
import { CAPACIDADES, montar } from "./test/soporte";
import type { ConversacionResumen } from "./types";

// ============================================================
// La lista de conversaciones propias: abrir el panel, buscar, renombrar y
// borrar una o todas (asistente-historial-conversaciones,
// asistente-superficie-frontend, tasks.md §10–§11).
// ============================================================

const UNA: ConversacionResumen = {
  id: "11111111-1111-4111-8111-111111111111",
  titulo: "¿Cuántos docentes hay?",
  creadoEn: "2026-01-10T10:00:00Z",
  ultimaActividad: "2026-01-10T10:05:00Z",
};

const OTRA: ConversacionResumen = {
  id: "22222222-2222-4222-8222-222222222222",
  titulo: "Pedidos de la cátedra",
  creadoEn: "2026-02-01T09:00:00Z",
  ultimaActividad: "2026-02-01T09:10:00Z",
};

beforeEach(() => {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
});

afterEach(() => {
  vi.restoreAllMocks();
});

async function abrirHistorial(user: ReturnType<typeof userEvent.setup>) {
  montar(<PanelDePrueba />);
  await user.click(await screen.findByRole("button", { name: "Historial" }));
}

describe("Abrir el panel de conversaciones propias", () => {
  it("renderiza las conversaciones, agrupadas por fecha relativa de última actividad", async () => {
    // UNA y OTRA son de comienzos de 2026: caen en «Anteriores» sea cuando sea
    // que corra este test. HOY se agrega con la fecha real, para el grupo que
    // sí depende de `Date.now()`.
    const HOY: ConversacionResumen = {
      id: "33333333-3333-4333-8333-333333333333",
      titulo: "¿Qué pasó hoy?",
      creadoEn: new Date().toISOString(),
      ultimaActividad: new Date().toISOString(),
    };
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([HOY, UNA, OTRA]);
    const user = userEvent.setup();

    await abrirHistorial(user);

    expect(await screen.findByText(HOY.titulo)).toBeInTheDocument();
    expect(screen.getByText(UNA.titulo)).toBeInTheDocument();
    expect(screen.getByText(OTRA.titulo)).toBeInTheDocument();
    expect(screen.getByText("Hoy")).toBeInTheDocument();
    expect(screen.getByText("Anteriores")).toBeInTheDocument();
    // No hay un grupo «Ayer» ni «Últimos 7 días» sin ninguna conversación de
    // esa fecha: un encabezado sin filas debajo no aporta nada.
    expect(screen.queryByText("Ayer")).toBeNull();
  });

  it("el título completo queda en el atributo `title`, para el hover", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const user = userEvent.setup();

    await abrirHistorial(user);

    expect(await screen.findByText(UNA.titulo)).toHaveAttribute("title", UNA.titulo);
  });

  it("sin conversaciones muestra un estado vacío", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([]);
    const user = userEvent.setup();

    await abrirHistorial(user);

    expect(
      await screen.findByText("Todavía no tenés conversaciones guardadas."),
    ).toBeInTheDocument();
  });
});

describe("Buscar en el historial", () => {
  it("una búsqueda usa el endpoint de búsqueda (debounced) y muestra sólo lo que coincide", async () => {
    const listar = vi
      .spyOn(historialApi, "listarConversaciones")
      .mockImplementation((q) =>
        Promise.resolve(q ? [OTRA].filter((c) => c.titulo.includes(q)) : [UNA, OTRA]),
      );
    const user = userEvent.setup();

    await abrirHistorial(user);
    await screen.findByText(UNA.titulo);

    await user.type(
      screen.getByRole("searchbox", { name: "Buscar en tus conversaciones" }),
      "Pedidos",
    );

    await waitFor(() => expect(listar).toHaveBeenLastCalledWith("Pedidos"));
    await waitFor(() => expect(screen.queryByText(UNA.titulo)).toBeNull());
    expect(screen.getByText(OTRA.titulo)).toBeInTheDocument();
  });

  it("sin coincidencias muestra un estado vacío de búsqueda", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockImplementation((q) =>
      Promise.resolve(q ? [] : [UNA]),
    );
    const user = userEvent.setup();

    await abrirHistorial(user);
    await screen.findByText(UNA.titulo);

    await user.type(
      screen.getByRole("searchbox", { name: "Buscar en tus conversaciones" }),
      "nada-que-coincida",
    );

    expect(
      await screen.findByText("Ninguna conversación coincide con esa búsqueda."),
    ).toBeInTheDocument();
  });
});

/** Abre el menú «⋮» de una fila —Renombrar y Borrar viven ahí, detrás del disparador. */
async function abrirAcciones(user: ReturnType<typeof userEvent.setup>, titulo: string) {
  await user.click(screen.getByRole("button", { name: `Acciones de «${titulo}»` }));
}

describe("Renombrar", () => {
  it("guarda el nuevo título y actualiza la lista sin recargar toda la página", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const renombrar = vi.spyOn(historialApi, "renombrarConversacion").mockResolvedValue(undefined);
    const user = userEvent.setup();

    await abrirHistorial(user);
    await screen.findByText(UNA.titulo);

    await abrirAcciones(user, UNA.titulo);
    await user.click(screen.getByRole("menuitem", { name: "Renombrar" }));
    const campo = screen.getByLabelText(`Nuevo título para «${UNA.titulo}»`);
    await user.clear(campo);
    await user.type(campo, "Docentes designados");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    expect(renombrar).toHaveBeenCalledWith(UNA.id, "Docentes designados");
    expect(await screen.findByText("Se guardó el nuevo título.")).toBeInTheDocument();
  });
});

describe("Borrar una conversación", () => {
  it("pide confirmación antes de borrar", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const eliminar = vi.spyOn(historialApi, "eliminarConversacion").mockResolvedValue(undefined);
    const user = userEvent.setup();

    await abrirHistorial(user);
    await screen.findByText(UNA.titulo);

    await abrirAcciones(user, UNA.titulo);
    await user.click(screen.getByRole("menuitem", { name: "Borrar" }));
    expect(eliminar).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Confirmar borrado" })).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Confirmar borrado" }));

    expect(eliminar).toHaveBeenCalledWith(UNA.id);
    expect(await screen.findByText("Se borró la conversación.")).toBeInTheDocument();
  });

  it("cancelar la confirmación no borra nada", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const eliminar = vi.spyOn(historialApi, "eliminarConversacion").mockResolvedValue(undefined);
    const user = userEvent.setup();

    await abrirHistorial(user);
    await screen.findByText(UNA.titulo);

    await abrirAcciones(user, UNA.titulo);
    await user.click(screen.getByRole("menuitem", { name: "Borrar" }));
    await user.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(eliminar).not.toHaveBeenCalled();
    expect(screen.getByText(UNA.titulo)).toBeInTheDocument();
  });
});

describe("Borrar todas las conversaciones", () => {
  it("pide confirmación y, tras confirmar, la lista queda vacía", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA, OTRA]);
    const eliminarTodo = vi
      .spyOn(historialApi, "eliminarTodasLasConversaciones")
      .mockResolvedValue(undefined);
    const user = userEvent.setup();

    await abrirHistorial(user);
    await screen.findByText(UNA.titulo);

    await user.click(screen.getByRole("button", { name: "Borrar todas" }));
    expect(eliminarTodo).not.toHaveBeenCalled();

    // Tras confirmar, `listarConversaciones` se invalida y vuelve a pedirse
    // vacía: es lo que ve la persona, sin recargar la página.
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([]);
    await user.click(screen.getByRole("button", { name: "Confirmar borrado de todo" }));

    expect(eliminarTodo).toHaveBeenCalledTimes(1);
    expect(
      await screen.findByText("Todavía no tenés conversaciones guardadas."),
    ).toBeInTheDocument();
  });

  it("deshabilitado cuando no hay ninguna conversación", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([]);
    const user = userEvent.setup();

    await abrirHistorial(user);

    expect(await screen.findByRole("button", { name: "Borrar todas" })).toBeDisabled();
  });
});

describe("Accesibilidad de la lista (tasks.md §14)", () => {
  it("cerrar, buscar, abrir, el «⋮» y borrar todas son alcanzables con Tab, en orden", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const user = userEvent.setup();

    await abrirHistorial(user);
    await screen.findByText(UNA.titulo);

    const panel = screen.getByLabelText("Tus conversaciones");
    const cerrar = within(panel).getByRole("button", { name: "Cerrar el historial" });
    const buscar = within(panel).getByRole("searchbox", { name: "Buscar en tus conversaciones" });
    const abrir = within(panel).getByText(UNA.titulo).closest("button");
    const acciones = within(panel).getByRole("button", { name: `Acciones de «${UNA.titulo}»` });
    const borrarTodas = within(panel).getByRole("button", { name: "Borrar todas" });
    expect(abrir).not.toBeNull();

    // El foco ya está en «Historial» —el clic de `abrirHistorial` lo dejó
    // ahí—, así que se tabula a partir de ahí y no desde el principio del
    // documento: lo que se afirma es el orden DENTRO del panel, no el de toda
    // la pantalla. «Historial» vive en el encabezado, así que lo primero
    // alcanzable adentro del panel es la «×» que lo cierra.
    await user.tab();
    expect(cerrar).toHaveFocus();
    await user.tab();
    expect(buscar).toHaveFocus();
    await user.tab();
    expect(abrir).toHaveFocus();
    await user.tab();
    expect(acciones).toHaveFocus();
    await user.tab();
    expect(borrarTodas).toHaveFocus();
  });

  it("cada acción activa con Enter/Espacio como con un clic", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const eliminar = vi.spyOn(historialApi, "eliminarConversacion").mockResolvedValue(undefined);
    const user = userEvent.setup();

    await abrirHistorial(user);
    await screen.findByText(UNA.titulo);

    screen.getByRole("button", { name: `Acciones de «${UNA.titulo}»` }).focus();
    await user.keyboard("{Enter}");
    screen.getByRole("menuitem", { name: "Borrar" }).focus();
    await user.keyboard("{Enter}");
    screen.getByRole("button", { name: "Confirmar borrado" }).focus();
    await user.keyboard(" ");

    expect(eliminar).toHaveBeenCalledWith(UNA.id);
  });
});
