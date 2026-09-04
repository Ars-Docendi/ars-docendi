import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, CanceledError } from "axios";

import { AsistentePage } from "./pages/AsistentePage";
import * as api from "./api/asistenteApi";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import { PanelDePrueba } from "./test/PanelDePrueba";
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
    montar(<PanelDePrueba />);

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
    montar(<PanelDePrueba />);

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

// ------------------------------------------------------------------ reintentar

describe("Reintentar", () => {
  it("reusa la clave de idempotencia y el texto del intento que falló", async () => {
    // La clave existe para esto: si el backend ya había terminado el turno cuando
    // se cortó la red, devuelve lo que guardó en lugar de cobrarle otra vez al
    // modelo. Con una clave nueva sería un turno más, y otro cobro.
    const user = userEvent.setup();
    const consultar = vi
      .spyOn(api, "consultar")
      .mockRejectedValueOnce(new Error("Network Error"))
      .mockResolvedValueOnce(respuesta());
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");
    await screen.findByText(/No pude completar la consulta/);

    await user.click(screen.getByRole("button", { name: "Reintentar" }));
    await screen.findByText("Hay 4 docentes designados.");

    expect(consultar).toHaveBeenCalledTimes(2);
    expect(consultar.mock.calls[1][1]).toBe(consultar.mock.calls[0][1]);
    expect(consultar.mock.calls[1][0].mensaje).toBe("algo");
    // El error se fue con el reintento: no queda una alerta vieja sobre la
    // respuesta nueva.
    expect(screen.queryByText(/No pude completar la consulta/)).toBeNull();
  });

  it("no se ofrece en vuelo ni sobre un turno que se dejó de esperar", async () => {
    // La idempotencia del backend consulta la caché ANTES de ejecutar y guarda
    // DESPUÉS, sin registrar el turno en curso: la misma clave mientras el original
    // sigue corriendo ejecuta el turno entero otra vez. Por eso sólo hay
    // «Reintentar» cuando el request terminó, y terminó mal.
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockImplementation(
      (_consulta, _clave, opciones) =>
        new Promise<RespuestaDelAsistente>((_, rechazar) => {
          opciones?.signal?.addEventListener("abort", () =>
            rechazar(new CanceledError("canceled")),
          );
        }),
    );
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");
    await screen.findByRole("button", { name: "Dejar de esperar" });
    expect(screen.queryByRole("button", { name: "Reintentar" })).toBeNull();

    await user.click(screen.getByRole("button", { name: "Dejar de esperar" }));
    await screen.findByText(/Dejaste de esperar/);

    expect(screen.queryByRole("button", { name: "Reintentar" })).toBeNull();
  });

  it("el timeout del cliente también lo ofrece", async () => {
    // A los 160 s el request terminó: el servidor ya cortó a los 150 y, si guardó
    // algo, la misma clave lo trae de vuelta sin volver a pagar.
    const user = userEvent.setup();
    const consultar = vi
      .spyOn(api, "consultar")
      .mockRejectedValueOnce(
        new AxiosError("timeout of 160000ms exceeded", AxiosError.ECONNABORTED),
      )
      .mockResolvedValueOnce(respuesta());
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");
    await screen.findByText(/tardó demasiado/);

    await user.click(screen.getByRole("button", { name: "Reintentar" }));
    await screen.findByText("Hay 4 docentes designados.");

    expect(consultar.mock.calls[1][1]).toBe(consultar.mock.calls[0][1]);
  });
});

// ---------------------------------------------------------- nueva conversación

describe("Nueva conversación", () => {
  it("vacía el hilo, arranca de cero y devuelve el foco al campo", async () => {
    // En la ruta el botón va en el encabezado de la página, fuera del panel.
    const user = userEvent.setup();
    const consultar = vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    montar(<AsistentePage />);

    const entrada = await screen.findByLabelText("Tu pregunta");
    await user.type(entrada, "primera{Enter}");
    await screen.findByText("Hay 4 docentes designados.");

    await user.click(screen.getByRole("button", { name: "Nueva conversación" }));

    expect(screen.queryByText("Hay 4 docentes designados.")).toBeNull();
    expect(screen.queryByText("primera")).toBeNull();
    // Sin confirmación: no hay nada persistido que perder.
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(entrada).toHaveFocus();

    await user.type(entrada, "segunda{Enter}");
    await waitFor(() => expect(consultar).toHaveBeenCalledTimes(2));
    expect(consultar.mock.calls[1][0].hilo).toBeNull();
  });

  it("está deshabilitada sin turnos y mientras hay uno en vuelo", async () => {
    // En el modal el botón va en el encabezado del panel.
    const user = userEvent.setup();
    let resolver: (valor: RespuestaDelAsistente) => void = () => {};
    vi.spyOn(api, "consultar").mockImplementationOnce(
      () => new Promise<RespuestaDelAsistente>((r) => (resolver = r)),
    );
    montar(<PanelDePrueba mostrarEncabezado />);

    const boton = await screen.findByRole("button", { name: "Nueva conversación" });
    expect(boton).toBeDisabled();

    await user.type(screen.getByLabelText("Tu pregunta"), "algo{Enter}");
    expect(boton).toBeDisabled();

    resolver(respuesta());
    await screen.findByText("Hay 4 docentes designados.");
    expect(boton).toBeEnabled();
  });
});

// ------------------------------------------------------------ dejar de esperar

describe("Dejar de esperar", () => {
  it("aborta el request, libera el campo y no lo presenta como error", async () => {
    const user = userEvent.setup();
    let señal: AbortSignal | undefined;
    vi.spyOn(api, "consultar").mockImplementation(
      (_consulta, _clave, opciones) =>
        new Promise<RespuestaDelAsistente>((_, rechazar) => {
          señal = opciones?.signal;
          // Lo que hace axios de verdad cuando la señal se aborta.
          señal?.addEventListener("abort", () => rechazar(new CanceledError("canceled")));
        }),
    );
    montar(<PanelDePrueba />);

    const entrada = await screen.findByLabelText("Tu pregunta");
    await user.type(entrada, "algo{Enter}");

    await user.click(await screen.findByRole("button", { name: "Dejar de esperar" }));

    expect(señal?.aborted).toBe(true);

    const aviso = await screen.findByText(/Dejaste de esperar la respuesta/);
    // Se dice lo que pasó de verdad: la consulta ya salió y el backend la sigue.
    // No es un error —lo pidió el usuario— y no se anuncia como tal.
    expect(aviso.textContent).toMatch(/cuenta para tu cupo/);
    expect(screen.queryByRole("alert")).toBeNull();
    expect(screen.queryByRole("button", { name: "Dejar de esperar" })).toBeNull();

    // El campo queda libre y con el foco, para la pregunta siguiente.
    expect(entrada).toHaveFocus();
    await user.type(entrada, "otra");
    expect(screen.getByRole("button", { name: "Enviar" })).toBeEnabled();
  });

  it("sin turno en vuelo no está", async () => {
    montar(<PanelDePrueba />);

    await screen.findByLabelText("Tu pregunta");

    expect(screen.queryByRole("button", { name: "Dejar de esperar" })).toBeNull();
  });
});
