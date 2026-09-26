import type { ComponentProps } from "react";
import { describe, it, expect, vi, afterEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { Mensaje } from "./components/Mensaje";
import * as api from "./api/asistenteApi";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import { PanelDePrueba } from "./test/PanelDePrueba";
import type { TurnoDeLaConversacion } from "./types";

// ============================================================
// Editar y reenviar la última pregunta (ARS-147,
// asistente-edicion-de-la-ultima-pregunta, design.md D9 de
// asistente-rediseno-v3).
//
// El mock trae un contador «N / M» y flechas para navegar entre versiones —
// RECHAZADO por decisión del producto: no hay historial de versiones en
// ningún lado del cliente, y varios tests de abajo lo verifican por su
// ausencia después de editar.
// ============================================================

afterEach(() => {
  vi.restoreAllMocks();
});

const PREGUNTA = "¿cuántos docentes hay?";

function unTurno(parcial: Partial<TurnoDeLaConversacion> = {}): TurnoDeLaConversacion {
  return { id: "t-1", pregunta: PREGUNTA, respuesta: respuesta(), ...parcial };
}

function montarMensaje(props: Partial<ComponentProps<typeof Mensaje>> = {}) {
  return montar(
    <ul>
      <Mensaje
        turno={unTurno()}
        onElegir={() => {}}
        onReintentar={() => {}}
        enVuelo={false}
        {...props}
      />
    </ul>,
  );
}

// --------------------------------------------- quién ofrece «Editar y reenviar»

describe("Sólo la última pregunta ofrece editar", () => {
  it("la última pregunta, sin nada en vuelo ni bloqueado, la ofrece", () => {
    userEvent.setup(); // instala un portapapeles de mentira, para «Copiar pregunta».
    montarMensaje({ esUltimo: true, onEditarYReenviar: () => {} });

    expect(screen.getByRole("button", { name: "Editar y reenviar" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Copiar pregunta" })).toBeInTheDocument();
  });

  it("una pregunta que no es la última sólo ofrece «Copiar pregunta»", () => {
    userEvent.setup();
    montarMensaje({ esUltimo: false, onEditarYReenviar: () => {} });

    expect(screen.getByRole("button", { name: "Copiar pregunta" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Editar y reenviar" })).toBeNull();
  });

  it("nada es editable mientras hay un turno en vuelo", () => {
    montarMensaje({ esUltimo: true, onEditarYReenviar: () => {}, enVuelo: true });

    expect(screen.queryByRole("button", { name: "Editar y reenviar" })).toBeNull();
  });

  it("bloqueado por cupo o mantenimiento tampoco la ofrece", () => {
    montarMensaje({ esUltimo: true, onEditarYReenviar: () => {}, bloqueado: true });

    expect(screen.queryByRole("button", { name: "Editar y reenviar" })).toBeNull();
  });

  it("sin `onEditarYReenviar` no hay control, aunque sea la última", () => {
    montarMensaje({ esUltimo: true });

    expect(screen.queryByRole("button", { name: "Editar y reenviar" })).toBeNull();
  });
});

// --------------------------------------------------------- el modo edición

describe("Editar inline", () => {
  it("abre un campo de ancho completo prellenado con la pregunta", async () => {
    const user = userEvent.setup();
    montarMensaje({ esUltimo: true, onEditarYReenviar: () => {} });

    await user.click(screen.getByRole("button", { name: "Editar y reenviar" }));

    const campo = screen.getByLabelText("Editar tu pregunta");
    expect(campo).toHaveValue(PREGUNTA);
    expect(screen.queryByRole("button", { name: "Editar y reenviar" })).toBeNull();
  });

  it("Escape cancela, restaura la pregunta y devuelve el foco a «Editar y reenviar»", async () => {
    const user = userEvent.setup();
    const alReenviar = vi.fn();
    montarMensaje({ esUltimo: true, onEditarYReenviar: alReenviar });

    await user.click(screen.getByRole("button", { name: "Editar y reenviar" }));
    const campo = screen.getByLabelText("Editar tu pregunta");
    await user.clear(campo);
    await user.type(campo, "otra cosa");
    await user.keyboard("{Escape}");

    expect(screen.queryByLabelText("Editar tu pregunta")).toBeNull();
    expect(screen.getByText(PREGUNTA)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Editar y reenviar" })).toHaveFocus();
    expect(alReenviar).not.toHaveBeenCalled();
  });

  it("«Cancelar» hace lo mismo que Escape", async () => {
    const user = userEvent.setup();
    montarMensaje({ esUltimo: true, onEditarYReenviar: () => {} });

    await user.click(screen.getByRole("button", { name: "Editar y reenviar" }));
    await user.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(screen.queryByLabelText("Editar tu pregunta")).toBeNull();
    expect(screen.getByRole("button", { name: "Editar y reenviar" })).toHaveFocus();
  });

  it("un campo vacío no envía nada con Enter, y el campo sigue abierto", async () => {
    const user = userEvent.setup();
    const alReenviar = vi.fn();
    montarMensaje({ esUltimo: true, onEditarYReenviar: alReenviar });

    await user.click(screen.getByRole("button", { name: "Editar y reenviar" }));
    await user.clear(screen.getByLabelText("Editar tu pregunta"));
    await user.keyboard("{Enter}");

    expect(alReenviar).not.toHaveBeenCalled();
    expect(screen.getByLabelText("Editar tu pregunta")).toBeInTheDocument();
  });

  it("Enter sin Shift reenvía la pregunta editada", async () => {
    const user = userEvent.setup();
    const alReenviar = vi.fn();
    montarMensaje({ esUltimo: true, onEditarYReenviar: alReenviar });

    await user.click(screen.getByRole("button", { name: "Editar y reenviar" }));
    const campo = screen.getByLabelText("Editar tu pregunta");
    await user.clear(campo);
    await user.type(campo, "¿y en Álgebra?");
    await user.keyboard("{Enter}");

    expect(alReenviar).toHaveBeenCalledWith("¿y en Álgebra?");
    expect(screen.queryByLabelText("Editar tu pregunta")).toBeNull();
  });

  it("Shift+Enter hace un salto de línea y no envía", async () => {
    const user = userEvent.setup();
    const alReenviar = vi.fn();
    montarMensaje({ esUltimo: true, onEditarYReenviar: alReenviar });

    await user.click(screen.getByRole("button", { name: "Editar y reenviar" }));
    const campo = screen.getByLabelText("Editar tu pregunta");
    await user.clear(campo);
    await user.type(campo, "línea uno{Shift>}{Enter}{/Shift}línea dos");

    expect(alReenviar).not.toHaveBeenCalled();
    expect(campo).toHaveValue("línea uno\nlínea dos");
  });

  it("el botón «Enviar» reenvía igual que Enter", async () => {
    const user = userEvent.setup();
    const alReenviar = vi.fn();
    montarMensaje({ esUltimo: true, onEditarYReenviar: alReenviar });

    await user.click(screen.getByRole("button", { name: "Editar y reenviar" }));
    const campo = screen.getByLabelText("Editar tu pregunta");
    await user.clear(campo);
    await user.type(campo, "¿y en Álgebra?");
    await user.click(screen.getByRole("button", { name: "Enviar" }));

    expect(alReenviar).toHaveBeenCalledWith("¿y en Álgebra?");
  });

  it("«Enviar» está deshabilitado con el campo vacío", async () => {
    const user = userEvent.setup();
    montarMensaje({ esUltimo: true, onEditarYReenviar: () => {} });

    await user.click(screen.getByRole("button", { name: "Editar y reenviar" }));
    await user.clear(screen.getByLabelText("Editar tu pregunta"));

    expect(screen.getByRole("button", { name: "Enviar" })).toBeDisabled();
  });

  it("la respuesta se atenúa mientras se edita, sin desaparecer", async () => {
    const user = userEvent.setup();
    montarMensaje({ esUltimo: true, onEditarYReenviar: () => {} });

    await user.click(screen.getByRole("button", { name: "Editar y reenviar" }));

    const respuestaDiv = screen
      .getByText("Hay 4 docentes designados.")
      .closest(".adoc-asistente-respuesta");
    expect(respuestaDiv).toHaveClass("adoc-asistente-respuesta--editando");
    expect(screen.getByText("Hay 4 docentes designados.")).toBeVisible();
  });

  it("nunca muestra un contador ni una navegación de versiones", async () => {
    const user = userEvent.setup();
    montarMensaje({ esUltimo: true, onEditarYReenviar: () => {} });

    await user.click(screen.getByRole("button", { name: "Editar y reenviar" }));
    const campo = screen.getByLabelText("Editar tu pregunta");
    await user.clear(campo);
    await user.type(campo, "otra pregunta");
    await user.keyboard("{Enter}");

    expect(screen.queryByText(/\d+\s*\/\s*\d+/)).toBeNull();
    expect(screen.queryByRole("button", { name: /versi[oó]n/i })).toBeNull();
  });
});

// ------------------------------------------------------------ punta a punta

describe("useAsistente.reenviarUltima, de punta a punta", () => {
  it("manda un turno nuevo con una clave nueva que nombra al viejo, y lo reemplaza en pantalla", async () => {
    const user = userEvent.setup();
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const consultar = vi
      .spyOn(api, "consultar")
      .mockResolvedValueOnce(respuesta({ respuesta: "Hay 4 docentes." }))
      .mockResolvedValueOnce(respuesta({ respuesta: "Hay 2 adjuntos." }));
    montar(<PanelDePrueba />);

    const entrada = await screen.findByLabelText("Tu pregunta");
    await user.type(entrada, `${PREGUNTA}{Enter}`);
    await screen.findByText("Hay 4 docentes.");

    await user.click(screen.getByRole("button", { name: "Editar y reenviar" }));
    const campo = screen.getByLabelText("Editar tu pregunta");
    await user.clear(campo);
    await user.type(campo, "¿cuántos adjuntos hay?");
    await user.keyboard("{Enter}");

    await screen.findByText("Hay 2 adjuntos.");

    // La pregunta y la respuesta reemplazadas no quedan en ningún lado.
    expect(screen.queryByText(PREGUNTA)).toBeNull();
    expect(screen.queryByText("Hay 4 docentes.")).toBeNull();

    expect(consultar).toHaveBeenCalledTimes(2);
    const [primerPedido, primeraClave] = consultar.mock.calls[0];
    const [segundoPedido, segundaClave] = consultar.mock.calls[1];

    expect(primerPedido.reemplaza).toBeUndefined();
    expect(segundoPedido.mensaje).toBe("¿cuántos adjuntos hay?");
    expect(segundoPedido.reemplaza).toBe(primeraClave);
    expect(segundaClave).not.toBe(primeraClave);

    // El foco vuelve al composer, como en cualquier turno que termina.
    expect(entrada).toHaveFocus();
  });

  it("una respuesta votada se resetea al reemplazarla: el voto no se arrastra", async () => {
    const user = userEvent.setup();
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    vi.spyOn(api, "consultar")
      .mockResolvedValueOnce(
        respuesta({ respuesta: "Hay 4 docentes.", claveDeRetroalimentacion: "tok-1" }),
      )
      .mockResolvedValueOnce(
        respuesta({ respuesta: "Hay 2 adjuntos.", claveDeRetroalimentacion: "tok-2" }),
      );
    montar(<PanelDePrueba />);

    const entrada = await screen.findByLabelText("Tu pregunta");
    await user.type(entrada, `${PREGUNTA}{Enter}`);
    await screen.findByText("Hay 4 docentes.");

    await user.click(screen.getByRole("button", { name: "Sirvió" }));
    expect(await screen.findByRole("button", { name: "Sirvió" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );

    await user.click(screen.getByRole("button", { name: "Editar y reenviar" }));
    const campo = screen.getByLabelText("Editar tu pregunta");
    await user.clear(campo);
    await user.type(campo, "¿cuántos adjuntos hay?");
    await user.keyboard("{Enter}");

    await screen.findByText("Hay 2 adjuntos.");

    // El turno nuevo trae SU propio token: el voto queda sin marcar.
    expect(screen.getByRole("button", { name: "Sirvió" })).toHaveAttribute("aria-pressed", "false");
  });

  it("«Reintentar» sobre un reemplazo fallido reusa la misma clave y el mismo objetivo", async () => {
    const user = userEvent.setup();
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const fallo = Object.assign(new Error("transporte"), { isAxiosError: true });
    const consultar = vi
      .spyOn(api, "consultar")
      .mockResolvedValueOnce(respuesta({ respuesta: "Hay 4 docentes." }))
      .mockRejectedValueOnce(fallo)
      .mockResolvedValueOnce(respuesta({ respuesta: "Hay 2 adjuntos." }));
    montar(<PanelDePrueba />);

    const entrada = await screen.findByLabelText("Tu pregunta");
    await user.type(entrada, `${PREGUNTA}{Enter}`);
    await screen.findByText("Hay 4 docentes.");

    await user.click(screen.getByRole("button", { name: "Editar y reenviar" }));
    const campo = screen.getByLabelText("Editar tu pregunta");
    await user.clear(campo);
    await user.type(campo, "¿cuántos adjuntos hay?");
    await user.keyboard("{Enter}");

    await screen.findByRole("button", { name: "Reintentar" });
    await user.click(screen.getByRole("button", { name: "Reintentar" }));

    await screen.findByText("Hay 2 adjuntos.");

    expect(consultar).toHaveBeenCalledTimes(3);
    const [, claveDelPrimero] = consultar.mock.calls[0];
    const [pedidoFallido, claveDelFallido] = consultar.mock.calls[1];
    const [pedidoDelReintento, claveDelReintento] = consultar.mock.calls[2];

    expect(pedidoFallido.reemplaza).toBe(claveDelPrimero);
    // El reintento manda la MISMA clave y el MISMO objetivo que el intento
    // que falló — a lo sumo un reemplazo se aplica de verdad.
    expect(claveDelReintento).toBe(claveDelFallido);
    expect(pedidoDelReintento.reemplaza).toBe(pedidoFallido.reemplaza);
  });
});
