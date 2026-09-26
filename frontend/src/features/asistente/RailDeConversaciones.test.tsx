import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { PanelDePrueba } from "./test/PanelDePrueba";
import * as api from "./api/asistenteApi";
import * as historialApi from "./api/historialApi";
import { CAPACIDADES, montar } from "./test/soporte";
import type { ConversacionResumen } from "./types";

// ============================================================
// El rail de conversaciones propias: siempre a la vista, colapsar/expandir,
// buscar, renombrar y borrar una o todas (asistente-historial-conversaciones,
// asistente-superficie-frontend, tasks.md §1).
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
  // Default inocuo: el doble clic que entra en edición dispara TAMBIÉN el
  // clic simple que abre la conversación —son dos eventos separados del
  // DOM, y el primero llega antes que el segundo—. Sin este default, ese
  // clic incidental golpea la red de verdad en los tests que sólo quieren
  // probar el renombre.
  vi.spyOn(historialApi, "reanudarConversacion").mockResolvedValue({
    hilo: "99999999-9999-4999-8999-999999999999",
    turnos: [],
  });
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("El rail está siempre a la vista, expandido por default", () => {
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

    montar(<PanelDePrueba />);

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

    montar(<PanelDePrueba />);

    expect(await screen.findByText(UNA.titulo)).toHaveAttribute("title", UNA.titulo);
  });

  it("sin conversaciones muestra un estado vacío", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([]);

    montar(<PanelDePrueba />);

    expect(
      await screen.findByText("Todavía no tenés conversaciones guardadas."),
    ).toBeInTheDocument();
  });
});

describe("Colapsar y expandir el rail (tasks.md 1.4, 1.6)", () => {
  it("colapsa a 60 px mostrando sólo expandir, «Nueva conversación» e «Historial»", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const user = userEvent.setup();
    montar(<PanelDePrueba />);
    await screen.findByText(UNA.titulo);

    await user.click(screen.getByRole("button", { name: "Colapsar conversaciones" }));

    expect(screen.getByRole("button", { name: "Expandir conversaciones" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Nueva conversación" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Historial" })).toBeInTheDocument();
    expect(screen.queryByText(UNA.titulo)).toBeNull();
    expect(screen.queryByRole("searchbox", { name: "Buscar en tus conversaciones" })).toBeNull();
  });

  it("colapsar mueve el foco a «Expandir conversaciones», y viceversa", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const user = userEvent.setup();
    montar(<PanelDePrueba />);
    await screen.findByText(UNA.titulo);

    await user.click(screen.getByRole("button", { name: "Colapsar conversaciones" }));

    const expandir = screen.getByRole("button", { name: "Expandir conversaciones" });
    expect(expandir).toHaveFocus();
    expect(expandir).toHaveAttribute("aria-expanded", "false");

    await user.click(expandir);

    const colapsar = screen.getByRole("button", { name: "Colapsar conversaciones" });
    expect(colapsar).toHaveFocus();
    expect(colapsar).toHaveAttribute("aria-expanded", "true");
  });

  it("«Historial», colapsado, también expande el rail", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const user = userEvent.setup();
    montar(<PanelDePrueba />);
    await screen.findByText(UNA.titulo);
    await user.click(screen.getByRole("button", { name: "Colapsar conversaciones" }));

    await user.click(screen.getByRole("button", { name: "Historial" }));

    expect(await screen.findByText(UNA.titulo)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Colapsar conversaciones" })).toBeInTheDocument();
  });
});

describe("Buscar en el rail", () => {
  it("una búsqueda usa el endpoint de búsqueda (debounced) y muestra sólo lo que coincide", async () => {
    const listar = vi
      .spyOn(historialApi, "listarConversaciones")
      .mockImplementation((q) =>
        Promise.resolve(q ? [OTRA].filter((c) => c.titulo.includes(q)) : [UNA, OTRA]),
      );
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
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

    montar(<PanelDePrueba />);
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

/** Abre el menú «⋮» de una fila —Renombrar y Eliminar viven ahí, detrás del disparador. */
async function abrirAcciones(user: ReturnType<typeof userEvent.setup>, titulo: string) {
  await user.click(screen.getByRole("button", { name: `Acciones de «${titulo}»` }));
}

describe("Renombrar", () => {
  it("desde el «⋮», guarda con Enter y actualiza la lista sin recargar toda la página", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const renombrar = vi.spyOn(historialApi, "renombrarConversacion").mockResolvedValue(undefined);
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
    await screen.findByText(UNA.titulo);

    await abrirAcciones(user, UNA.titulo);
    await user.click(screen.getByRole("menuitem", { name: "Renombrar" }));
    const campo = screen.getByLabelText(`Nuevo título para «${UNA.titulo}»`);
    await user.clear(campo);
    await user.type(campo, "Docentes designados{Enter}");

    expect(renombrar).toHaveBeenCalledWith(UNA.id, "Docentes designados");
    expect(await screen.findByText("Se guardó el nuevo título.")).toBeInTheDocument();
  });

  it("por doble clic en el título, también entra en edición", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
    await screen.findByText(UNA.titulo);

    await user.dblClick(screen.getByText(UNA.titulo));

    expect(screen.getByLabelText(`Nuevo título para «${UNA.titulo}»`)).toBeInTheDocument();
  });

  it("Escape cancela sin guardar y conserva el título anterior", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const renombrar = vi.spyOn(historialApi, "renombrarConversacion").mockResolvedValue(undefined);
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
    await screen.findByText(UNA.titulo);

    await abrirAcciones(user, UNA.titulo);
    await user.click(screen.getByRole("menuitem", { name: "Renombrar" }));
    const campo = screen.getByLabelText(`Nuevo título para «${UNA.titulo}»`);
    await user.clear(campo);
    await user.type(campo, "algo que no se guarda{Escape}");

    expect(renombrar).not.toHaveBeenCalled();
    expect(await screen.findByText(UNA.titulo)).toBeInTheDocument();
  });

  it("un título vacío conserva el anterior", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const renombrar = vi.spyOn(historialApi, "renombrarConversacion").mockResolvedValue(undefined);
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
    await screen.findByText(UNA.titulo);

    await abrirAcciones(user, UNA.titulo);
    await user.click(screen.getByRole("menuitem", { name: "Renombrar" }));
    const campo = screen.getByLabelText(`Nuevo título para «${UNA.titulo}»`);
    await user.clear(campo);
    await user.keyboard("{Enter}");

    expect(renombrar).not.toHaveBeenCalled();
    expect(await screen.findByText(UNA.titulo)).toBeInTheDocument();
  });
});

describe("Eliminar una conversación", () => {
  it("pide confirmación antes de borrar", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const eliminar = vi.spyOn(historialApi, "eliminarConversacion").mockResolvedValue(undefined);
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
    await screen.findByText(UNA.titulo);

    await abrirAcciones(user, UNA.titulo);
    await user.click(screen.getByRole("menuitem", { name: "Eliminar" }));
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

    montar(<PanelDePrueba />);
    await screen.findByText(UNA.titulo);

    await abrirAcciones(user, UNA.titulo);
    await user.click(screen.getByRole("menuitem", { name: "Eliminar" }));
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

    montar(<PanelDePrueba />);
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

    montar(<PanelDePrueba />);

    expect(await screen.findByRole("button", { name: "Borrar todas" })).toBeDisabled();
  });
});

describe("Accesibilidad del rail (tasks.md §14)", () => {
  it("cada acción activa con Enter/Espacio como con un clic", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    const eliminar = vi.spyOn(historialApi, "eliminarConversacion").mockResolvedValue(undefined);
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
    await screen.findByText(UNA.titulo);

    screen.getByRole("button", { name: `Acciones de «${UNA.titulo}»` }).focus();
    await user.keyboard("{Enter}");
    screen.getByRole("menuitem", { name: "Eliminar" }).focus();
    await user.keyboard("{Enter}");
    screen.getByRole("button", { name: "Confirmar borrado" }).focus();
    await user.keyboard(" ");

    expect(eliminar).toHaveBeenCalledWith(UNA.id);
  });

  it("la fila activa se marca con `aria-current`", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    vi.spyOn(historialApi, "reanudarConversacion").mockResolvedValue({
      hilo: "44444444-4444-4444-8444-444444444444",
      turnos: [],
    });
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
    await user.click(await screen.findByText(UNA.titulo));

    await waitFor(() =>
      expect(screen.getByText(UNA.titulo)).toHaveAttribute("aria-current", "true"),
    );
  });
});
