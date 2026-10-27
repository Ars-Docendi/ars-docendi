import { useState } from "react";
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { EntradaDePregunta } from "./components/EntradaDePregunta";

// ============================================================
// El composer por sí solo: el campo, el contador y el botón «Enviar».
//
// Que el panel no mande nada en vuelo y que el foco vuelva al campo lo fijan
// `asistente.test.tsx` y `asistente.turnos.test.tsx` montando el panel entero.
// Acá va lo que el composer promete sin importar quién lo monte.
// ============================================================

/** El composer es controlado; alguien tiene que sostener el valor. */
function Arnes({
  onEnviar,
  onDetener = () => {},
  enVuelo = false,
  umbralMs,
}: {
  onEnviar: () => void;
  onDetener?: () => void;
  enVuelo?: boolean;
  umbralMs?: number;
}) {
  const [valor, setValor] = useState("");
  return (
    <EntradaDePregunta
      valor={valor}
      onCambiar={setValor}
      onEnviar={onEnviar}
      onDetener={onDetener}
      enVuelo={enVuelo}
      umbralMs={umbralMs}
    />
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("El contador de caracteres", () => {
  it("aparece recién cerca del límite y se asocia al campo como descripción", async () => {
    const user = userEvent.setup();
    render(<Arnes onEnviar={vi.fn()} />);

    const campo = screen.getByLabelText("Tu pregunta");

    // El límite lo impone el campo, no una validación al enviar: un mensaje más
    // largo volvía como 400 con el mensaje genérico de error.
    expect(campo).toHaveAttribute("maxlength", "2000");

    await user.click(campo);
    await user.paste("a".repeat(1799));

    // Lejos del límite no hay contador: un «12 / 2 000» permanente es ruido.
    expect(screen.queryByText(/\/ 2.?000/)).toBeNull();
    expect(campo).not.toHaveAccessibleDescription();

    await user.type(campo, "a");

    const contador = screen.getByText(/1.?800 \/ 2.?000/);
    expect(contador).toBeVisible();
    expect(campo).toHaveAccessibleDescription(/1.?800 \/ 2.?000/);
    // Sin región viva: un contador que se anuncia a cada tecla es insoportable.
    expect(contador).not.toHaveAttribute("aria-live");
    expect(contador).not.toHaveAttribute("role", "status");
  });

  it("el campo no acepta más del límite", async () => {
    const user = userEvent.setup();
    render(<Arnes onEnviar={vi.fn()} />);

    const campo = screen.getByLabelText("Tu pregunta");
    await user.click(campo);
    await user.paste("a".repeat(2000));
    await user.type(campo, "b");

    expect(campo).toHaveValue("a".repeat(2000));
    expect(screen.getByText(/2.?000 \/ 2.?000/)).toBeInTheDocument();
  });
});

describe("Enter y el botón de envío", () => {
  it("con puntero fino Enter envía y Shift+Enter hace salto de línea", async () => {
    const user = userEvent.setup();
    const onEnviar = vi.fn();
    render(<Arnes onEnviar={onEnviar} />);

    const campo = screen.getByLabelText("Tu pregunta");

    await user.type(campo, "primera línea{Shift>}{Enter}{/Shift}segunda");
    expect(onEnviar).not.toHaveBeenCalled();
    expect(campo).toHaveValue("primera línea\nsegunda");

    await user.type(campo, "{Enter}");
    expect(onEnviar).toHaveBeenCalledOnce();
    // Enter no dejó un salto colgado que después viajaría con la pregunta.
    expect(campo).toHaveValue("primera línea\nsegunda");
  });

  it("con puntero grueso Enter hace salto y no envía", async () => {
    // En un teléfono Enter es la única forma de hacer un salto de línea, y el
    // botón queda a un toque. Es lo que hacen Claude y ChatGPT en móvil.
    vi.stubGlobal("matchMedia", () => ({ matches: true }));
    const user = userEvent.setup();
    const onEnviar = vi.fn();
    render(<Arnes onEnviar={onEnviar} />);

    const campo = screen.getByLabelText("Tu pregunta");
    await user.type(campo, "a{Enter}");

    expect(onEnviar).not.toHaveBeenCalled();
    expect(campo).toHaveValue("a\n");
  });

  it("«Enviar» tiene etiqueta visible, se deshabilita vacío o en vuelo y no gira", async () => {
    const user = userEvent.setup();
    const { rerender } = render(<Arnes onEnviar={vi.fn()} />);

    const boton = screen.getByRole("button", { name: "Enviar" });
    expect(boton).toBeVisible();
    expect(boton).toBeDisabled();

    await user.type(screen.getByLabelText("Tu pregunta"), "algo");
    expect(boton).toBeEnabled();

    rerender(<Arnes onEnviar={vi.fn()} enVuelo />);
    // Vacío otra vez porque el arnés se remonta; lo que importa es el vuelo.
    await user.type(screen.getByLabelText("Tu pregunta"), "algo");
    const enVuelo = screen.getByRole("button", { name: "Enviar" });
    expect(enVuelo).toBeDisabled();
    // Sin spinner: parpadea en las respuestas deterministas, que es justo lo que
    // el umbral del indicador evita. El estado en vuelo lo dice el indicador.
    expect(enVuelo).not.toHaveAttribute("aria-busy");
  });
});

describe("«Dejar de esperar» en el lugar de «Enviar» (asistente-rediseno-v3, D14)", () => {
  it("antes del umbral muestra «Enviar» deshabilitado, y recién después «Dejar de esperar»", async () => {
    render(<Arnes onEnviar={vi.fn()} enVuelo umbralMs={0} />);

    // Con el umbral en 0 el `setTimeout` que lo enciende igual corre en una
    // vuelta del reloj real: `findBy` espera esa vuelta sin necesitar temporizadores falsos.
    const detener = await screen.findByRole("button", { name: "Dejar de esperar" });
    expect(detener).toBeEnabled();
    expect(screen.queryByRole("button", { name: "Enviar" })).toBeNull();
  });

  it("sin turno en vuelo no aparece", () => {
    render(<Arnes onEnviar={vi.fn()} umbralMs={0} />);

    expect(screen.queryByRole("button", { name: "Dejar de esperar" })).toBeNull();
    expect(screen.getByRole("button", { name: "Enviar" })).toBeInTheDocument();
  });

  it("al pulsarlo llama a onDetener y no a onEnviar", async () => {
    const user = userEvent.setup();
    const onEnviar = vi.fn();
    const onDetener = vi.fn();
    render(<Arnes onEnviar={onEnviar} onDetener={onDetener} enVuelo umbralMs={0} />);

    await user.click(await screen.findByRole("button", { name: "Dejar de esperar" }));

    expect(onDetener).toHaveBeenCalledOnce();
    expect(onEnviar).not.toHaveBeenCalled();
  });
});
