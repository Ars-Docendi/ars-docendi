import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { PanelDePrueba } from "./test/PanelDePrueba";
import * as api from "./api/asistenteApi";
import * as historialApi from "./api/historialApi";
import { CAPACIDADES, montar } from "./test/soporte";
import type { ConversacionResumen } from "./types";

// ============================================================
// Anuncios de renombrar/eliminar/reanudar por la región viva EXISTENTE, sin
// desorientar el foco (asistente-accesibilidad, tasks.md §1, §14.2). El rail
// ya no es un cajón que se abre y cierra: está siempre a la vista, así que
// estos tests ya no necesitan abrir nada antes de operar sobre una fila.
// ============================================================

const UNA: ConversacionResumen = {
  id: "11111111-1111-4111-8111-111111111111",
  titulo: "¿Cuántos docentes hay?",
  creadoEn: "2026-01-10T10:00:00Z",
  ultimaActividad: "2026-01-10T10:05:00Z",
};

beforeEach(() => {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
});

afterEach(() => {
  vi.restoreAllMocks();
});

function regionViva(): HTMLElement {
  return screen.getByRole("log", { name: "Conversación con el asistente" });
}

describe("Eliminar una conversación", () => {
  it("anuncia por la región viva existente y el foco no se pierde en <body>", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    vi.spyOn(historialApi, "eliminarConversacion").mockResolvedValue(undefined);
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
    await screen.findByText(UNA.titulo);

    await user.click(screen.getByRole("button", { name: `Acciones de «${UNA.titulo}»` }));
    await user.click(screen.getByRole("menuitem", { name: "Eliminar" }));
    await user.click(screen.getByRole("button", { name: "Confirmar borrado" }));

    const anuncio = await screen.findByText("Se borró la conversación.");
    expect(regionViva().contains(anuncio)).toBe(true);
    expect(document.activeElement).not.toBe(document.body);
  });
});

describe("Eliminar todas las conversaciones", () => {
  it("anuncia por la región viva existente y el foco no se pierde en <body>", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    vi.spyOn(historialApi, "eliminarTodasLasConversaciones").mockResolvedValue(undefined);
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
    await screen.findByText(UNA.titulo);

    await user.click(screen.getByRole("button", { name: "Borrar todas" }));
    await user.click(screen.getByRole("button", { name: "Confirmar borrado de todo" }));

    const anuncio = await screen.findByText("Se borraron todas tus conversaciones.");
    expect(regionViva().contains(anuncio)).toBe(true);
    expect(document.activeElement).not.toBe(document.body);
  });
});

describe("Renombrar", () => {
  it("anuncia por la región viva existente y el foco no se pierde en <body>", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    vi.spyOn(historialApi, "renombrarConversacion").mockResolvedValue(undefined);
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
    await screen.findByText(UNA.titulo);

    await user.click(screen.getByRole("button", { name: `Acciones de «${UNA.titulo}»` }));
    await user.click(screen.getByRole("menuitem", { name: "Renombrar" }));
    await user.keyboard(" (editado){Enter}");

    const anuncio = await screen.findByText("Se guardó el nuevo título.");
    expect(regionViva().contains(anuncio)).toBe(true);
    expect(document.activeElement).not.toBe(document.body);
  });
});

describe("Reanudar", () => {
  it("anuncia que la conversación está lista y el foco queda en el campo de la pregunta, no en <body>", async () => {
    vi.spyOn(historialApi, "listarConversaciones").mockResolvedValue([UNA]);
    vi.spyOn(historialApi, "reanudarConversacion").mockResolvedValue({
      hilo: "22222222-2222-4222-8222-222222222222",
      turnos: [
        {
          id: "33333333-3333-4333-8333-333333333333",
          pregunta: UNA.titulo,
          sql: null,
          estado: "respondida",
          ocurrioEn: UNA.ultimaActividad,
        },
      ],
    });
    const user = userEvent.setup();

    montar(<PanelDePrueba />);
    await user.click(await screen.findByText(UNA.titulo));

    const anuncio = await screen.findByText("La conversación está lista.");
    expect(regionViva().contains(anuncio)).toBe(true);
    await waitFor(() => expect(document.activeElement).toBe(screen.getByLabelText("Tu pregunta")));
  });
});
