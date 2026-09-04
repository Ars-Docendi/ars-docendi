import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { PanelAsistente } from "./components/PanelAsistente";
import * as api from "./api/asistenteApi";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import type { RespuestaDelAsistente } from "./types";

beforeEach(() => {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ------------------------------------------------------------ un turno a la vez

describe("Un turno a la vez", () => {
  it("Enter en vuelo no manda un segundo turno", async () => {
    // Dos pedidos concurrentes son dos claves de idempotencia —dos cobros— y el
    // segundo sale con el hilo viejo o nulo: abre otra conversación sin que nadie
    // la haya pedido. Sólo el botón estaba deshabilitado; Enter no miraba nada.
    const user = userEvent.setup();
    let resolver: (valor: RespuestaDelAsistente) => void = () => {};
    const consultar = vi
      .spyOn(api, "consultar")
      .mockImplementationOnce(() => new Promise<RespuestaDelAsistente>((r) => (resolver = r)))
      .mockResolvedValue(respuesta());
    montar(<PanelAsistente />);

    const entrada = await screen.findByLabelText("Tu pregunta");

    await user.type(entrada, "a{Enter}");
    await user.type(entrada, "b{Enter}");

    expect(consultar).toHaveBeenCalledTimes(1);
    // Se puede seguir escribiendo mientras tanto: lo que no se mandó no se pierde.
    expect(entrada).toHaveValue("b");

    resolver(respuesta());
    await screen.findByText("Hay 4 docentes designados.");

    expect(entrada).toHaveValue("b");

    // Terminado el turno, Enter vuelve a enviar, con una clave nueva.
    await user.type(entrada, "{Enter}");
    await waitFor(() => expect(consultar).toHaveBeenCalledTimes(2));
    expect(consultar.mock.calls[1][0].mensaje).toBe("b");
    expect(consultar.mock.calls[1][1]).not.toEqual(consultar.mock.calls[0][1]);
  });
});

// ------------------------------------------------------------- el hilo perdido

describe("Un hilo perdido", () => {
  it("un 404 reinicia el hilo: el turno siguiente arranca una conversación nueva", async () => {
    // El backend expira el hilo por inactividad y responde 404. Si el cliente
    // sigue mandando el mismo identificador, cada pregunta vuelve a fallar igual y
    // el aviso «Volvé a hacer la pregunta» es una promesa que no se cumple.
    const user = userEvent.setup();
    const hiloA = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    const consultar = vi
      .spyOn(api, "consultar")
      .mockResolvedValueOnce(respuesta({ hilo: hiloA }))
      .mockRejectedValueOnce(
        Object.assign(new Error("Not Found"), { isAxiosError: true, response: { status: 404 } }),
      )
      .mockResolvedValueOnce(respuesta());
    montar(<PanelAsistente />);

    const entrada = await screen.findByLabelText("Tu pregunta");

    await user.type(entrada, "primera{Enter}");
    await screen.findByText("Hay 4 docentes designados.");

    await user.type(entrada, "segunda{Enter}");
    await screen.findByText(/Se perdió el hilo/);
    expect(consultar.mock.calls[1][0].hilo).toBe(hiloA);

    await user.type(entrada, "tercera{Enter}");
    await waitFor(() => expect(consultar).toHaveBeenCalledTimes(3));

    expect(consultar.mock.calls[2][0].hilo).toBeNull();
  });
});
