import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { ListaDeModelosEnLaNube, type GrupoDeModelosEnLaNube } from "./ListaDeModelosEnLaNube";

const GRUPOS: GrupoDeModelosEnLaNube[] = [
  {
    empresa: "anthropic",
    nombre: "Anthropic",
    credencial: { estado: "guardada", pista: "a1f9" },
    modelos: ["claude-sonnet-5", "claude-haiku-4-5"],
  },
  {
    empresa: "openai",
    nombre: "OpenAI",
    credencial: { estado: "faltante", pista: null },
    modelos: ["gpt-5", "gpt-5-mini"],
  },
];

function montar(
  props: Partial<Parameters<typeof ListaDeModelosEnLaNube>[0]> = {},
  alElegir = vi.fn(),
) {
  render(
    <>
      <span id="etiqueta">Modelo</span>
      <button type="button">afuera</button>
      <ListaDeModelosEnLaNube
        id="lista"
        etiquetadaPor="etiqueta"
        grupos={GRUPOS}
        seleccion={{ empresa: "anthropic", modelo: "claude-sonnet-5" }}
        enUso={{ empresa: "openai", modelo: "gpt-5" }}
        onElegir={alElegir}
        {...props}
      />
    </>,
  );
  return { alElegir, boton: screen.getByRole("button", { name: /claude-sonnet-5/ }) };
}

/** El texto de la opción resaltada, según `aria-activedescendant`. */
function opcionResaltada(): string | null {
  const lista = screen.getByRole("listbox");
  return (
    document.getElementById(lista.getAttribute("aria-activedescendant") ?? "")?.textContent ?? null
  );
}

describe("ListaDeModelosEnLaNube", () => {
  it("cerrada, muestra el modelo elegido con su empresa y el estado de su clave", () => {
    const { boton } = montar();

    expect(boton).toHaveTextContent("Anthropic · clave ••••a1f9");
    expect(boton).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("abierta, agrupa por empresa, marca el elegido y el que está en uso", async () => {
    const user = userEvent.setup();
    const { boton } = montar();

    await user.click(boton);

    expect(screen.getByRole("group", { name: "Anthropic, clave ••••a1f9" })).toBeInTheDocument();
    expect(screen.getByRole("group", { name: "OpenAI, falta la clave" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "claude-sonnet-5" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(screen.getByRole("option", { name: "gpt-5-mini" })).toHaveAttribute(
      "aria-selected",
      "false",
    );
    expect(screen.getByRole("option", { name: /^gpt-5\s*En uso$/ })).toBeInTheDocument();
  });

  it("al abrir, el foco pasa a la lista y la opción resaltada es la elegida", async () => {
    const user = userEvent.setup();
    const { boton } = montar();

    await user.click(boton);

    expect(screen.getByRole("listbox")).toHaveFocus();
    expect(opcionResaltada()).toBe("claude-sonnet-5");
  });

  it("las flechas mueven la resaltada, cruzan grupos y se detienen en los extremos", async () => {
    const user = userEvent.setup();
    const { boton } = montar();
    await user.click(boton);

    await user.keyboard("{ArrowDown}");
    expect(opcionResaltada()).toBe("claude-haiku-4-5");
    await user.keyboard("{ArrowDown}");
    expect(opcionResaltada()).toBe("gpt-5En uso");
    await user.keyboard("{End}");
    expect(opcionResaltada()).toBe("gpt-5-mini");
    await user.keyboard("{ArrowDown}");
    expect(opcionResaltada()).toBe("gpt-5-mini");
    await user.keyboard("{Home}");
    expect(opcionResaltada()).toBe("claude-sonnet-5");
    await user.keyboard("{ArrowUp}");
    expect(opcionResaltada()).toBe("claude-sonnet-5");
  });

  it("Enter elige la resaltada, cierra la lista y devuelve el foco al botón", async () => {
    const user = userEvent.setup();
    const { boton, alElegir } = montar();
    await user.click(boton);

    await user.keyboard("{ArrowDown}{ArrowDown}{ArrowDown}{Enter}");

    expect(alElegir).toHaveBeenCalledExactlyOnceWith("openai", "gpt-5-mini");
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    expect(boton).toHaveFocus();
  });

  it("Esc cierra sin elegir y devuelve el foco al botón", async () => {
    const user = userEvent.setup();
    const { boton, alElegir } = montar();
    await user.click(boton);

    await user.keyboard("{ArrowDown}{Escape}");

    expect(alElegir).not.toHaveBeenCalled();
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    expect(boton).toHaveFocus();
  });

  it("un clic afuera cierra sin elegir", async () => {
    const user = userEvent.setup();
    const { boton, alElegir } = montar();
    await user.click(boton);

    await user.click(screen.getByRole("button", { name: "afuera" }));

    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    expect(alElegir).not.toHaveBeenCalled();
    expect(boton).toHaveAttribute("aria-expanded", "false");
  });

  it("un clic en una opción la elige", async () => {
    const user = userEvent.setup();
    const { boton, alElegir } = montar();
    await user.click(boton);

    await user.click(screen.getByRole("option", { name: "gpt-5-mini" }));

    expect(alElegir).toHaveBeenCalledExactlyOnceWith("openai", "gpt-5-mini");
  });

  it("la flecha abajo sobre el botón cerrado abre la lista", async () => {
    const user = userEvent.setup();
    const { boton } = montar();
    boton.focus();

    await user.keyboard("{ArrowDown}");

    expect(screen.getByRole("listbox")).toBeInTheDocument();
    expect(boton).toHaveAttribute("aria-expanded", "true");
  });
});
