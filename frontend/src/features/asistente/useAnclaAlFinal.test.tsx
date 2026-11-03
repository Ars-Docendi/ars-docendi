import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { fireEvent, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import * as api from "./api/asistenteApi";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import { PanelDePrueba } from "./test/PanelDePrueba";
import type { RespuestaDelAsistente } from "./types";

// ============================================================
// El scroll del hilo: sigue a quien está abajo y no arrastra a quien subió.
//
// jsdom no calcula layout: `scrollHeight`, `clientHeight` y `offsetTop` valen
// cero siempre. Se fingen sobre el elemento —el hilo mide 300 con 1 000 de
// contenido— y sobre el prototipo —la tarjeta de respuesta empieza a 480—, que
// es lo mínimo para que las tres reglas del anclaje tengan contra qué medirse.
// ============================================================

const CONTENIDO = 1000;
const VENTANA = 300;
const FONDO = CONTENIDO - VENTANA;
const INICIO_DE_LA_TARJETA = 480;

beforeEach(() => {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
  vi.spyOn(HTMLElement.prototype, "offsetTop", "get").mockImplementation(function (
    this: HTMLElement,
  ) {
    return this.classList.contains("adoc-asistente-respuesta") ? INICIO_DE_LA_TARJETA : 0;
  });
});

afterEach(() => {
  vi.restoreAllMocks();
});

/** El panel con un turno en vuelo, y el hilo con sus medidas fingidas. */
async function montarConTurnoEnVuelo() {
  const user = userEvent.setup();
  let resolver: (valor: RespuestaDelAsistente) => void = () => {};
  vi.spyOn(api, "consultar").mockImplementationOnce(
    () => new Promise<RespuestaDelAsistente>((r) => (resolver = r)),
  );
  const { container } = montar(<PanelDePrueba />);
  const entrada = await screen.findByLabelText("Tu pregunta");

  const hilo = container.querySelector<HTMLElement>(".adoc-asistente-hilo");
  if (!hilo) throw new Error("El panel no tiene hilo.");
  Object.defineProperty(hilo, "scrollHeight", { value: CONTENIDO, configurable: true });
  Object.defineProperty(hilo, "clientHeight", { value: VENTANA, configurable: true });

  await user.type(entrada, "algo{Enter}");

  return { user, hilo, responder: () => resolver(respuesta()) };
}

describe("El hilo anclado", () => {
  it("al enviar va al fondo, aunque el usuario hubiera subido", async () => {
    // La pregunta recién enviada y el indicador tienen que verse: es lo único que
    // justifica mover a alguien que estaba leyendo otra cosa.
    const { hilo } = await montarConTurnoEnVuelo();

    expect(hilo.scrollTop).toBe(FONDO);
    expect(screen.queryByRole("button", { name: "Ir al final" })).toBeNull();
  });

  it("no arrastra a quien subió: llega la respuesta, el scroll no se mueve y aparece «Ir al final»", async () => {
    const { user, hilo, responder } = await montarConTurnoEnVuelo();

    // El usuario sube a releer algo.
    hilo.scrollTop = 0;
    fireEvent.scroll(hilo);

    responder();
    await screen.findByText("Hay 4 docentes designados.");

    expect(hilo.scrollTop).toBe(0);
    const boton = screen.getByRole("button", { name: "Ir al final" });
    // Es un control del panel, no un mensaje: fuera de la región viva.
    expect(screen.getByRole("log").contains(boton)).toBe(false);

    await user.click(boton);

    expect(hilo.scrollTop).toBe(FONDO);
    expect(screen.queryByRole("button", { name: "Ir al final" })).toBeNull();
  });

  it("anclado abajo, sigue: la respuesta se ve desde su inicio y no hay botón", async () => {
    // Al inicio de la tarjeta y no al fondo: con una tabla larga, el fondo deja el
    // texto de la respuesta arriba, fuera de vista.
    const { hilo, responder } = await montarConTurnoEnVuelo();
    expect(hilo.scrollTop).toBe(FONDO);

    responder();
    await screen.findByText("Hay 4 docentes designados.");

    expect(hilo.scrollTop).toBe(INICIO_DE_LA_TARJETA);
    expect(screen.queryByRole("button", { name: "Ir al final" })).toBeNull();
  });

  it("a 24 px del fondo todavía cuenta como anclado; a 25, ya no", async () => {
    const { hilo, responder } = await montarConTurnoEnVuelo();

    hilo.scrollTop = FONDO - 25;
    fireEvent.scroll(hilo);
    expect(screen.getByRole("button", { name: "Ir al final" })).toBeInTheDocument();

    hilo.scrollTop = FONDO - 24;
    fireEvent.scroll(hilo);
    expect(screen.queryByRole("button", { name: "Ir al final" })).toBeNull();

    responder();
    await screen.findByText("Hay 4 docentes designados.");

    expect(hilo.scrollTop).toBe(INICIO_DE_LA_TARJETA);
  });
});
