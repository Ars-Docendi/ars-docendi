import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AxiosError, type AxiosResponse } from "axios";

import * as api from "../api/administracionAsistenteApi";
import type { EstadoDelModelo } from "../types";
import { PanelModeloDelAsistente } from "./PanelModeloDelAsistente";

function estadoBase(cambios: Partial<EstadoDelModelo> = {}): EstadoDelModelo {
  return {
    enUso: { origen: "servidor", empresa: "ollama", modelo: "qwen3.5:9b" },
    servidoresLocales: ["ollama", "local"],
    catalogo: [
      {
        empresa: "anthropic",
        tipo: "nube",
        credencial: { estado: "guardada", pista: "a1f9" },
        modelos: ["claude-sonnet-5", "claude-haiku-4-5"],
      },
      {
        empresa: "openai",
        tipo: "nube",
        credencial: { estado: "faltante", pista: null },
        modelos: ["gpt-5", "gpt-5-mini"],
      },
      {
        empresa: "ollama",
        tipo: "local",
        credencial: null,
        modelos: ["qwen3.5:9b", "gpt-oss:20b"],
      },
      { empresa: "local", tipo: "local", credencial: null, modelos: ["qwen3-8b"] },
    ],
    cifradoDisponible: true,
    ...cambios,
  };
}

function errorHttp(estado: number, codigo: string) {
  return new AxiosError("falló", "ERR_BAD_REQUEST", undefined, undefined, {
    status: estado,
    data: { codigo },
  } as AxiosResponse);
}

function montar(cambios?: React.ReactNode) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={cliente}>
      <PanelModeloDelAsistente cambios={cambios} />
    </QueryClientProvider>,
  );
}

const botonAplicar = () => screen.getByRole("button", { name: "Aplicar cambio" });
const botonProbar = () => screen.getByRole("button", { name: /Probar conexión|Probando/ });

/** Pasa a «API en la nube» y abre la lista de modelos. */
async function irALaNube(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole("radio", { name: "API en la nube" }));
}

async function elegirModeloEnLaNube(user: ReturnType<typeof userEvent.setup>, modelo: string) {
  await user.click(screen.getByRole("button", { name: /^Modelo\s/ }));
  await user.click(screen.getByRole("option", { name: new RegExp(`^${modelo}$`) }));
}

beforeEach(() => {
  vi.spyOn(api, "obtenerEstadoDelModelo").mockResolvedValue(estadoBase());
});

afterEach(() => vi.restoreAllMocks());

describe("PanelModeloDelAsistente — qué está en uso", () => {
  it("muestra el modelo en uso y su origen, sin ofrecer volver cuando viene del servidor", async () => {
    montar();

    const banner = await screen.findByRole("group", { name: "Modelo en uso" });
    expect(banner).toHaveTextContent("En uso: ollama/qwen3.5:9b");
    expect(banner).toHaveTextContent("Configuración del servidor");
    expect(
      screen.queryByRole("button", { name: "Volver a la configuración del servidor" }),
    ).not.toBeInTheDocument();
  });

  it("marca «En uso» sólo en la tarjeta de la modalidad vigente", async () => {
    montar();

    const local = (await screen.findByRole("radio", { name: "Modelo local" })).closest(
      ".sistema-tarjeta",
    ) as HTMLElement;
    const nube = screen
      .getByRole("radio", { name: "API en la nube" })
      .closest(".sistema-tarjeta") as HTMLElement;
    expect(within(local).getByText("En uso")).toBeInTheDocument();
    expect(within(nube).queryByText("En uso")).not.toBeInTheDocument();
    expect(within(nube).getByText(/1 de 2 empresas con clave guardada/)).toBeInTheDocument();
  });

  it("avisa que el enmascaramiento queda activo cuando la selección de pantalla es una API en la nube", async () => {
    vi.spyOn(api, "obtenerEstadoDelModelo").mockResolvedValue(
      estadoBase({
        enUso: { origen: "pantalla", empresa: "anthropic", modelo: "claude-sonnet-5" },
      }),
    );

    montar();

    const banner = await screen.findByRole("group", { name: "Modelo en uso" });
    expect(banner).toHaveTextContent("datos personales viajan siempre enmascarados");
    expect(screen.getByRole("radio", { name: "API en la nube" })).toBeChecked();
  });

  it("si el estado no carga, lo dice y permite reintentar", async () => {
    vi.spyOn(api, "obtenerEstadoDelModelo").mockRejectedValueOnce(new Error("boom"));
    const user = userEvent.setup();
    montar();

    expect(
      await screen.findByText("No se pudo cargar el modelo del asistente."),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Reintentar" }));

    expect(await screen.findByRole("group", { name: "Modelo en uso" })).toBeInTheDocument();
  });
});

describe("PanelModeloDelAsistente — modelo local", () => {
  it("lista sólo los servidores que devuelve el backend, por nombre, y no tiene campo de URL ni de clave", async () => {
    montar();

    const servidor = await screen.findByRole("combobox", { name: /Servidor/ });
    expect(
      within(servidor)
        .getAllByRole("option")
        .map((o) => o.textContent),
    ).toEqual(["Servicio de inferencia compartido", "Servidor propio"]);
    expect(screen.getByRole("combobox", { name: /Modelo/ })).toHaveValue("qwen3.5:9b");
    expect(screen.queryByLabelText(/url|clave|contraseña/i)).not.toBeInTheDocument();
    expect(screen.getByRole("tooltip", { hidden: true })).toHaveTextContent(
      "se configuran ahí junto al servidor",
    );
  });

  it("con un único servidor configurado, sólo ese aparece", async () => {
    vi.spyOn(api, "obtenerEstadoDelModelo").mockResolvedValue(
      estadoBase({ servidoresLocales: ["local"] }),
    );

    montar();

    const servidor = await screen.findByRole("combobox", { name: /Servidor/ });
    expect(within(servidor).getAllByRole("option")).toHaveLength(1);
    expect(within(servidor).getByRole("option")).toHaveTextContent("Servidor propio");
  });

  it("sin servidores locales configurados, la tarjeta local no se puede elegir", async () => {
    vi.spyOn(api, "obtenerEstadoDelModelo").mockResolvedValue(
      estadoBase({
        servidoresLocales: [],
        enUso: { origen: "servidor", empresa: "simulado", modelo: "" },
      }),
    );

    montar();

    expect(await screen.findByRole("radio", { name: "Modelo local" })).toBeDisabled();
    expect(screen.getByRole("radio", { name: "API en la nube" })).toBeChecked();
  });
});

describe("PanelModeloDelAsistente — probar y aplicar", () => {
  it("«Aplicar cambio» está deshabilitado hasta que la prueba pasa, y se deshabilita de nuevo al cambiar la selección", async () => {
    const probar = vi
      .spyOn(api, "probarModelo")
      .mockResolvedValue({ exito: true, latenciaMs: 412, codigo: null });
    const user = userEvent.setup();
    montar();

    await screen.findByRole("combobox", { name: /Servidor/ });
    expect(botonAplicar()).toBeDisabled();

    await user.click(botonProbar());

    expect(await screen.findByText("Conexión correcta.")).toBeInTheDocument();
    expect(screen.getByText(/respondió en 412 ms/)).toBeInTheDocument();
    expect(probar).toHaveBeenCalledWith({ empresa: "ollama", modelo: "qwen3.5:9b" });
    expect(botonAplicar()).toBeEnabled();

    await user.selectOptions(screen.getByRole("combobox", { name: /Modelo/ }), "gpt-oss:20b");

    expect(botonAplicar()).toBeDisabled();
    expect(screen.queryByText("Conexión correcta.")).not.toBeInTheDocument();
  });

  it("también se deshabilita al cambiar de servidor o de modalidad", async () => {
    vi.spyOn(api, "probarModelo").mockResolvedValue({ exito: true, latenciaMs: 10, codigo: null });
    const user = userEvent.setup();
    montar();
    await screen.findByRole("combobox", { name: /Servidor/ });

    await user.click(botonProbar());
    await waitFor(() => expect(botonAplicar()).toBeEnabled());
    await user.selectOptions(screen.getByRole("combobox", { name: /Servidor/ }), "local");
    expect(botonAplicar()).toBeDisabled();

    await user.click(botonProbar());
    await waitFor(() => expect(botonAplicar()).toBeEnabled());
    await irALaNube(user);
    expect(botonAplicar()).toBeDisabled();
  });

  it("una prueba fallida dice por qué, no guarda nada y no habilita aplicar", async () => {
    vi.spyOn(api, "probarModelo").mockResolvedValue({
      exito: false,
      latenciaMs: null,
      codigo: "inalcanzable",
    });
    const user = userEvent.setup();
    montar();
    await screen.findByRole("combobox", { name: /Servidor/ });

    await user.click(botonProbar());

    const alerta = await screen.findByRole("alert");
    expect(alerta).toHaveTextContent("La prueba falló.");
    expect(alerta).toHaveTextContent("No se pudo conectar con el servidor.");
    expect(alerta).toHaveTextContent("No se guardó nada.");
    expect(botonAplicar()).toBeDisabled();
  });

  it("`saturado` se muestra como «servicio ocupado», no como una prueba fallida", async () => {
    vi.spyOn(api, "probarModelo").mockResolvedValue({
      exito: false,
      latenciaMs: null,
      codigo: "saturado",
    });
    const user = userEvent.setup();
    montar();
    await screen.findByRole("combobox", { name: /Servidor/ });

    await user.click(botonProbar());

    expect(
      await screen.findByText("El servicio está ocupado, probá en unos segundos."),
    ).toBeInTheDocument();
    expect(screen.queryByText("La prueba falló.")).not.toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(botonAplicar()).toBeDisabled();
  });

  it("aplicar pide confirmación que avisa que los turnos en curso terminan con el modelo actual, y recién ahí cambia", async () => {
    vi.spyOn(api, "probarModelo").mockResolvedValue({ exito: true, latenciaMs: 20, codigo: null });
    const aplicar = vi.spyOn(api, "aplicarModelo").mockResolvedValue({
      enUso: { origen: "pantalla", empresa: "ollama", modelo: "gpt-oss:20b" },
      latenciaMs: 20,
      pista: null,
    });
    vi.mocked(api.obtenerEstadoDelModelo)
      .mockResolvedValueOnce(estadoBase())
      .mockResolvedValue(
        estadoBase({ enUso: { origen: "pantalla", empresa: "ollama", modelo: "gpt-oss:20b" } }),
      );
    const user = userEvent.setup();
    montar();
    await screen.findByRole("combobox", { name: /Servidor/ });
    await user.selectOptions(screen.getByRole("combobox", { name: /Modelo/ }), "gpt-oss:20b");
    await user.click(botonProbar());
    await waitFor(() => expect(botonAplicar()).toBeEnabled());

    await user.click(botonAplicar());

    expect(aplicar).not.toHaveBeenCalled();
    const confirmacion = screen
      .getByText("¿Cambiar el modelo?")
      .closest(".adoc-alert") as HTMLElement;
    expect(confirmacion).toHaveTextContent("Los turnos en curso terminan con ollama/qwen3.5:9b");
    expect(screen.getByRole("button", { name: "Confirmar cambio" })).toHaveFocus();

    await user.click(screen.getByRole("button", { name: "Confirmar cambio" }));

    await waitFor(() =>
      expect(aplicar).toHaveBeenCalledWith({ empresa: "ollama", modelo: "gpt-oss:20b" }),
    );
    const banner = await screen.findByRole("group", { name: "Modelo en uso" });
    await waitFor(() => expect(banner).toHaveTextContent("En uso: ollama/gpt-oss:20b"));
    expect(screen.getByRole("status", { name: "" })).toHaveTextContent(
      "Modelo cambiado a ollama/gpt-oss:20b. Rige desde el próximo turno.",
    );
    expect(botonAplicar()).toBeDisabled();
    expect(botonProbar()).toHaveFocus();
  });

  it("cancelar la confirmación no cambia nada y devuelve el foco a «Aplicar cambio»", async () => {
    vi.spyOn(api, "probarModelo").mockResolvedValue({ exito: true, latenciaMs: 20, codigo: null });
    const aplicar = vi.spyOn(api, "aplicarModelo");
    const user = userEvent.setup();
    montar();
    await screen.findByRole("combobox", { name: /Servidor/ });
    await user.click(botonProbar());
    await waitFor(() => expect(botonAplicar()).toBeEnabled());
    await user.click(botonAplicar());

    await user.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(aplicar).not.toHaveBeenCalled();
    expect(botonAplicar()).toHaveFocus();
  });

  it("si el servidor rechaza al aplicar (422), no cambia nada y lo explica", async () => {
    vi.spyOn(api, "probarModelo").mockResolvedValue({ exito: true, latenciaMs: 20, codigo: null });
    vi.spyOn(api, "aplicarModelo").mockRejectedValue(errorHttp(422, "sin-credito"));
    const user = userEvent.setup();
    montar();
    await screen.findByRole("combobox", { name: /Servidor/ });
    await user.click(botonProbar());
    await waitFor(() => expect(botonAplicar()).toBeEnabled());
    await user.click(botonAplicar());
    await user.click(screen.getByRole("button", { name: "Confirmar cambio" }));

    const alerta = await screen.findByRole("alert");
    expect(alerta).toHaveTextContent("La cuenta del proveedor no tiene crédito.");
    expect(screen.getByRole("group", { name: "Modelo en uso" })).toHaveTextContent(
      "En uso: ollama/qwen3.5:9b",
    );
    expect(botonAplicar()).toBeDisabled();
  });

  it("sin clave maestra (503), guardar una clave se explica y los modelos locales siguen disponibles", async () => {
    vi.spyOn(api, "probarModelo").mockRejectedValue(errorHttp(503, "clave-maestra-no-disponible"));
    const user = userEvent.setup();
    montar();
    await screen.findByRole("combobox", { name: /Servidor/ });

    await user.click(botonProbar());

    const alerta = await screen.findByRole("alert");
    expect(alerta).toHaveTextContent("Guardar claves no está disponible");
    expect(alerta).toHaveTextContent("Los modelos locales siguen disponibles");
  });

  it("un 403 se dice como falta de permiso, sin texto crudo", async () => {
    vi.spyOn(api, "probarModelo").mockRejectedValue(errorHttp(403, "x"));
    const user = userEvent.setup();
    montar();
    await screen.findByRole("combobox", { name: /Servidor/ });

    await user.click(botonProbar());

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "No tenés permiso para administrar el modelo.",
    );
  });
});

describe("PanelModeloDelAsistente — API en la nube y claves", () => {
  it("una empresa sin clave pide «Clave de {empresa}», no se puede probar sin ella y la manda como claveNueva", async () => {
    const probar = vi
      .spyOn(api, "probarModelo")
      .mockResolvedValue({ exito: true, latenciaMs: 300, codigo: null });
    const user = userEvent.setup();
    montar();
    await irALaNube(user);

    await elegirModeloEnLaNube(user, "gpt-5-mini");

    const campo = screen.getByLabelText(/Clave de OpenAI/);
    expect(campo).toHaveAttribute("type", "password");
    expect(botonProbar()).toBeDisabled();

    await user.type(campo, "sk-secreta-1234");
    await user.click(botonProbar());

    await waitFor(() =>
      expect(probar).toHaveBeenCalledWith({
        empresa: "openai",
        modelo: "gpt-5-mini",
        claveNueva: "sk-secreta-1234",
      }),
    );
    await waitFor(() => expect(botonAplicar()).toBeEnabled());

    // Cambiar la clave invalida la prueba: lo que se probó era otra clave.
    await user.type(campo, "x");
    expect(botonAplicar()).toBeDisabled();
  });

  it("con clave guardada muestra sólo la pista y «Reemplazar», sin campo de clave", async () => {
    const user = userEvent.setup();
    montar();
    await irALaNube(user);

    const fila = await screen.findByRole("group", { name: "Clave de Anthropic" });
    expect(fila).toHaveTextContent("••••a1f9");
    expect(within(fila).getByRole("button", { name: "Reemplazar" })).toBeInTheDocument();
    expect(screen.queryByPlaceholderText("Pegá la clave de API")).not.toBeInTheDocument();
  });

  it("sin clave maestra, una empresa sin clave no deja escribir ni probar", async () => {
    vi.spyOn(api, "obtenerEstadoDelModelo").mockResolvedValue(
      estadoBase({ cifradoDisponible: false }),
    );
    const user = userEvent.setup();
    montar();
    await irALaNube(user);

    await elegirModeloEnLaNube(user, "gpt-5");

    expect(screen.getByLabelText(/Clave de OpenAI/)).toBeDisabled();
    expect(screen.getByText(/Guardar claves no está disponible/)).toBeInTheDocument();
    expect(botonProbar()).toBeDisabled();
  });

  it("aplicar una empresa nueva guarda su clave y avisa que se guardará cifrada", async () => {
    vi.spyOn(api, "probarModelo").mockResolvedValue({ exito: true, latenciaMs: 10, codigo: null });
    const aplicar = vi.spyOn(api, "aplicarModelo").mockResolvedValue({
      enUso: { origen: "pantalla", empresa: "openai", modelo: "gpt-5" },
      latenciaMs: 10,
      pista: "1234",
    });
    const user = userEvent.setup();
    montar();
    await irALaNube(user);
    await elegirModeloEnLaNube(user, "gpt-5");
    await user.type(screen.getByLabelText(/Clave de OpenAI/), "sk-secreta-1234");
    await user.click(botonProbar());
    await waitFor(() => expect(botonAplicar()).toBeEnabled());

    await user.click(botonAplicar());
    expect(screen.getByText(/Se guardará la clave de OpenAI, cifrada/)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Confirmar cambio" }));

    await waitFor(() =>
      expect(aplicar).toHaveBeenCalledWith({
        empresa: "openai",
        modelo: "gpt-5",
        claveNueva: "sk-secreta-1234",
      }),
    );
  });
});

describe("PanelModeloDelAsistente — reemplazo de clave", () => {
  async function abrirElReemplazo(user: ReturnType<typeof userEvent.setup>) {
    await irALaNube(user);
    await user.click(await screen.findByRole("button", { name: "Reemplazar" }));
    return screen.getByRole("group", { name: "Reemplazar la clave de Anthropic" });
  }

  it("la clave vigente sigue visible y en uso mientras el panel está abierto", async () => {
    const user = userEvent.setup();
    montar();

    const panel = await abrirElReemplazo(user);

    expect(screen.getByRole("group", { name: "Clave de Anthropic" })).toHaveTextContent("••••a1f9");
    expect(screen.getByText(/Sigue en uso hasta que confirmes el reemplazo/)).toBeInTheDocument();
    expect(within(panel).getByLabelText(/Nueva clave de API/)).toHaveFocus();
    expect(screen.queryByRole("button", { name: "Reemplazar" })).not.toBeInTheDocument();
  });

  it("un reemplazo que falla deja la clave vieja visible y el panel abierto, y lo dice", async () => {
    const reemplazar = vi
      .spyOn(api, "reemplazarClaveDeEmpresa")
      .mockRejectedValue(errorHttp(422, "clave-invalida"));
    const user = userEvent.setup();
    montar();
    const panel = await abrirElReemplazo(user);

    await user.type(within(panel).getByLabelText(/Nueva clave de API/), "sk-nueva-9z9z");
    await user.click(within(panel).getByRole("button", { name: "Confirmar reemplazo" }));

    expect(reemplazar).toHaveBeenCalledWith("anthropic", "sk-nueva-9z9z");
    const alerta = await within(panel).findByRole("alert");
    expect(alerta).toHaveTextContent("No se reemplazó.");
    expect(alerta).toHaveTextContent("El proveedor rechazó la clave.");
    expect(alerta).toHaveTextContent("La clave ••••a1f9 sigue en uso.");
    expect(screen.getByRole("group", { name: "Clave de Anthropic" })).toHaveTextContent("••••a1f9");
    expect(panel).toBeInTheDocument();
  });

  it("un reemplazo que pasa actualiza la pista, cierra el panel, anuncia y devuelve el foco", async () => {
    vi.spyOn(api, "reemplazarClaveDeEmpresa").mockResolvedValue({
      empresa: "anthropic",
      pista: "9z9z",
    });
    const conPistaNueva = estadoBase();
    conPistaNueva.catalogo[0].credencial = { estado: "guardada", pista: "9z9z" };
    vi.mocked(api.obtenerEstadoDelModelo)
      .mockResolvedValueOnce(estadoBase())
      .mockResolvedValue(conPistaNueva);
    const user = userEvent.setup();
    montar();
    const panel = await abrirElReemplazo(user);

    await user.type(within(panel).getByLabelText(/Nueva clave de API/), "sk-nueva-9z9z");
    await user.click(within(panel).getByRole("button", { name: "Confirmar reemplazo" }));

    await waitFor(() =>
      expect(screen.getByRole("group", { name: "Clave de Anthropic" })).toHaveTextContent(
        "••••9z9z",
      ),
    );
    expect(
      screen.queryByRole("group", { name: "Reemplazar la clave de Anthropic" }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole("status", { name: "" })).toHaveTextContent(
      "Clave de Anthropic reemplazada. Ahora termina en 9z9z.",
    );
    expect(screen.getByRole("button", { name: "Reemplazar" })).toHaveFocus();
  });

  it("cancelar descarta lo escrito y devuelve el foco a «Reemplazar»", async () => {
    const reemplazar = vi.spyOn(api, "reemplazarClaveDeEmpresa");
    const user = userEvent.setup();
    montar();
    const panel = await abrirElReemplazo(user);
    await user.type(within(panel).getByLabelText(/Nueva clave de API/), "sk-a-medias");

    await user.click(within(panel).getByRole("button", { name: "Cancelar" }));

    expect(reemplazar).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Reemplazar" })).toHaveFocus();
    await user.click(screen.getByRole("button", { name: "Reemplazar" }));
    expect(screen.getByLabelText(/Nueva clave de API/)).toHaveValue("");
  });
});

describe("PanelModeloDelAsistente — volver a la configuración del servidor", () => {
  it("con una selección de pantalla ofrece volver, lo hace y anuncia", async () => {
    const restablecer = vi.spyOn(api, "restablecerModelo").mockResolvedValue();
    vi.mocked(api.obtenerEstadoDelModelo)
      .mockResolvedValueOnce(
        estadoBase({
          enUso: { origen: "pantalla", empresa: "anthropic", modelo: "claude-sonnet-5" },
        }),
      )
      .mockResolvedValue(estadoBase());
    const user = userEvent.setup();
    montar();

    await user.click(
      await screen.findByRole("button", { name: "Volver a la configuración del servidor" }),
    );

    await waitFor(() => expect(restablecer).toHaveBeenCalledOnce());
    await waitFor(() =>
      expect(screen.getByRole("group", { name: "Modelo en uso" })).toHaveTextContent(
        "Configuración del servidor",
      ),
    );
    expect(screen.getByRole("status", { name: "" })).toHaveTextContent(
      "Se volvió a la configuración del servidor.",
    );
    expect(screen.getByRole("radio", { name: "Modelo local" })).toBeChecked();
  });

  it("si falla, lo dice y la selección sigue en uso", async () => {
    vi.spyOn(api, "restablecerModelo").mockRejectedValue(errorHttp(500, "x"));
    vi.spyOn(api, "obtenerEstadoDelModelo").mockResolvedValue(
      estadoBase({
        enUso: { origen: "pantalla", empresa: "anthropic", modelo: "claude-sonnet-5" },
      }),
    );
    const user = userEvent.setup();
    montar();

    await user.click(
      await screen.findByRole("button", { name: "Volver a la configuración del servidor" }),
    );

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "No se pudo volver a la configuración del servidor.",
    );
  });
});

describe("PanelModeloDelAsistente — cambios del modelo", () => {
  it("compone el listado que le entrega la sección Sistema", async () => {
    montar(<p>Listado de cambios</p>);

    expect(await screen.findByText("Listado de cambios")).toBeInTheDocument();
  });
});
