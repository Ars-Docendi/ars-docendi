import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { PanelPestana, Pestanas } from "./Pestanas";

const ITEMS = [
  { id: "a", label: "Uno" },
  { id: "b", label: "Dos", disabled: true },
  { id: "c", label: "Tres" },
  { id: "d", label: "Cuatro" },
];

function Demo() {
  const [activa, setActiva] = useState("a");
  return (
    <>
      <Pestanas
        prefijo="demo"
        items={ITEMS}
        value={activa}
        onChange={setActiva}
        aria-label="Demo"
      />
      <PanelPestana prefijo="demo" id={activa}>
        Panel {activa}
      </PanelPestana>
    </>
  );
}

describe("Pestanas", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("con movimiento reducido el clic desplaza sin animación", async () => {
    vi.stubGlobal("matchMedia", (consulta: string) => ({
      matches: consulta.includes("prefers-reduced-motion"),
    }));
    const desplazar = vi.fn();
    Element.prototype.scrollIntoView = desplazar;
    render(<Demo />);

    await userEvent.click(screen.getByRole("tab", { name: "Tres" }));

    expect(desplazar).toHaveBeenCalledWith(expect.objectContaining({ behavior: "auto" }));
  });

  it("Fin e Inicio mueven el foco y activan; las flechas saltean las deshabilitadas", async () => {
    render(<Demo />);
    const uno = screen.getByRole("tab", { name: "Uno" });
    uno.focus();

    await userEvent.keyboard("{End}");
    expect(screen.getByRole("tab", { name: "Cuatro" })).toHaveFocus();
    expect(screen.getByRole("tab", { name: "Cuatro" })).toHaveAttribute("aria-selected", "true");

    await userEvent.keyboard("{Home}");
    expect(uno).toHaveFocus();
    await userEvent.keyboard("{ArrowRight}");
    expect(screen.getByRole("tab", { name: "Tres" })).toHaveFocus();
  });

  it("sólo la pestaña activa se tabula y su panel está etiquetado por ella", () => {
    render(<Demo />);
    expect(screen.getByRole("tab", { name: "Uno" })).toHaveAttribute("tabindex", "0");
    expect(screen.getByRole("tab", { name: "Tres" })).toHaveAttribute("tabindex", "-1");
    expect(screen.getByRole("tabpanel", { name: "Uno" })).toHaveTextContent("Panel a");
    const controla = screen.getByRole("tab", { name: "Uno" }).getAttribute("aria-controls")!;
    expect(document.getElementById(controla)).not.toBeNull();
  });
});
