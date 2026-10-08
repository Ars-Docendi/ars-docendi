import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError } from "axios";

import * as api from "./api/asistenteApi";
import { apiClient } from "../../shared/api/client";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import { PanelDePrueba } from "./test/PanelDePrueba";

beforeEach(() => {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
});

afterEach(() => {
  vi.restoreAllMocks();
});

// --------------------------------------------------------- señal y tope de tiempo

describe("Todo turno lleva señal de aborto y un tope de tiempo del cliente", () => {
  it("el turno viaja con una señal de aborto propia", async () => {
    // Sin señal no hay forma de soltar un request desde afuera: el turno queda en
    // vuelo hasta que el servidor conteste, si es que contesta.
    const user = userEvent.setup();
    const consultar = vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");
    await screen.findByText("Hay 4 docentes designados.");

    expect(consultar.mock.calls[0][2]?.signal).toBeInstanceOf(AbortSignal);
  });

  it("el pedido HTTP lleva el timeout del turno, y el cliente compartido ninguno", async () => {
    // El backend acota el turno a 150 s; el cliente espera apenas más, para que el
    // que corte sea el servidor con su mensaje. Y va POR REQUEST: el resto de la
    // aplicación no tiene turnos de dos minutos y medio.
    const user = userEvent.setup();
    const post = vi.spyOn(apiClient, "post").mockResolvedValue({ data: respuesta() });
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");
    await screen.findByText("Hay 4 docentes designados.");

    expect(post).toHaveBeenCalledOnce();
    const opciones = post.mock.calls[0][2];
    expect(opciones).toMatchObject({ timeout: 160_000 });
    expect(opciones?.signal).toBeInstanceOf(AbortSignal);
    expect(apiClient.defaults.timeout ?? 0).toBe(0);
  });

  it("un timeout del cliente se muestra como que tardó demasiado, sin códigos", async () => {
    // Hoy el timeout cae en la misma rama que «sin respuesta»: le dice al usuario
    // que revise su conexión cuando la conexión está bien y lo que pasó es que la
    // pregunta era demasiado grande para el presupuesto.
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockRejectedValue(
      new AxiosError("timeout of 160000ms exceeded", AxiosError.ECONNABORTED),
    );
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");

    const alerta = await screen.findByText(/tardó demasiado/);

    expect(alerta).toBeInTheDocument();
    expect(alerta.textContent).not.toMatch(/ECONNABORTED|timeout|160/);
    expect(screen.queryByText(/Revisá tu conexión/)).toBeNull();
  });
});
