import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { AxiosProgressEvent, AxiosRequestConfig } from "axios";

import * as api from "./api/asistenteApi";
import { apiClient } from "../../shared/api/client";
import { mensajeDeError } from "./errores";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import { PanelDePrueba } from "./test/PanelDePrueba";

// ============================================================
// La redacción por fragmentos (asistente-optimizaciones-modelo-local, D9):
// el turno se pide a `POST /consultas/flujo` sólo si las capacidades lo
// ofrecen, el texto parcial se muestra mientras llega y la respuesta completa
// lo reemplaza.
// ============================================================

beforeEach(() => {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
});

afterEach(() => {
  vi.restoreAllMocks();
});

function evento(nombre: string, datos: unknown): string {
  return `event: ${nombre}\ndata: ${JSON.stringify(datos)}\n\n`;
}

describe("leerEventosDelFlujo", () => {
  it("junta los fragmentos y deja el resultado crudo", () => {
    const texto =
      evento("redaccion", { texto: "Hay " }) +
      evento("redaccion", { texto: "4 docentes." }) +
      evento("resultado", respuesta());

    const eventos = api.leerEventosDelFlujo(texto);

    expect(eventos.redaccion).toBe("Hay 4 docentes.");
    expect(JSON.parse(eventos.resultado!)).toEqual(respuesta());
    expect(eventos.errorStatus).toBeUndefined();
  });

  it("ignora un evento que todavía no terminó de llegar", () => {
    const texto = evento("redaccion", { texto: "Hay " }) + 'event: redaccion\ndata: {"tex';

    expect(api.leerEventosDelFlujo(texto).redaccion).toBe("Hay ");
  });

  it("lee el status de un error", () => {
    expect(api.leerEventosDelFlujo(evento("error", { status: 500 })).errorStatus).toBe(500);
  });
});

describe("consultarEnFlujo", () => {
  /** Simula axios: un progreso con el texto parcial y la respuesta entera. */
  function servidorQueManda(parcial: string, completo: string) {
    return vi
      .spyOn(apiClient, "post")
      .mockImplementation((_url: string, _cuerpo: unknown, config?: AxiosRequestConfig) => {
        config?.onDownloadProgress?.({
          event: { target: { responseText: parcial } },
        } as unknown as AxiosProgressEvent);
        return Promise.resolve({ data: completo, config, request: {} });
      });
  }

  it("pasa la redacción acumulada y devuelve el resultado", async () => {
    const fragmentos = evento("redaccion", { texto: "Hay " });
    const post = servidorQueManda(
      fragmentos,
      fragmentos + evento("redaccion", { texto: "4." }) + evento("resultado", respuesta()),
    );
    const alRecibirRedaccion = vi.fn();

    const resultado = await api.consultarEnFlujo({ mensaje: "algo" }, "clave", {
      alRecibirRedaccion,
    });

    expect(resultado).toEqual(respuesta());
    expect(alRecibirRedaccion).toHaveBeenCalledWith("Hay ");
    const [ruta, , opciones] = post.mock.calls[0];
    expect(ruta).toBe("/api/asistente/consultas/flujo");
    expect(opciones).toMatchObject({
      adapter: "xhr",
      timeout: 160_000,
      headers: { "Idempotency-Key": "clave" },
    });
  });

  it("un error dentro del flujo se lee como el error HTTP de siempre", async () => {
    servidorQueManda("", evento("error", { status: 500 }));

    const fallo = await api
      .consultarEnFlujo({ mensaje: "algo" }, "clave", { alRecibirRedaccion: vi.fn() })
      .catch((error: unknown) => error);

    expect(mensajeDeError(fallo)).toBe(
      "El asistente tuvo un problema al responder. Probá de nuevo en un momento.",
    );
  });
});

describe("El turno con la redacción por fragmentos", () => {
  it("muestra el texto parcial y lo reemplaza por la respuesta completa", async () => {
    const user = userEvent.setup();
    let terminar: (valor: ReturnType<typeof respuesta>) => void = () => {};
    const enFlujo = vi
      .spyOn(api, "consultarEnFlujo")
      .mockImplementation((_consulta, _clave, { alRecibirRedaccion }) => {
        alRecibirRedaccion("Hay 4 doc");
        return new Promise((resolver) => {
          terminar = resolver;
        });
      });
    const consultar = vi.spyOn(api, "consultar");
    montar(<PanelDePrueba redaccionEnFlujo />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");

    expect(await screen.findByText("Hay 4 doc")).toBeInTheDocument();

    terminar(respuesta());

    expect(await screen.findByText("Hay 4 docentes designados.")).toBeInTheDocument();
    expect(screen.queryByText("Hay 4 doc")).toBeNull();
    expect(enFlujo).toHaveBeenCalledOnce();
    expect(consultar).not.toHaveBeenCalled();
  });

  it("sin la capacidad, el turno se pide entero como siempre", async () => {
    const user = userEvent.setup();
    const consultar = vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    const enFlujo = vi.spyOn(api, "consultarEnFlujo");
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");
    await screen.findByText("Hay 4 docentes designados.");

    expect(consultar).toHaveBeenCalledOnce();
    expect(enFlujo).not.toHaveBeenCalled();
  });
});
