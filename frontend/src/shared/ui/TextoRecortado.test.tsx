import { afterEach, describe, expect, it } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import { TextoRecortado } from "./TextoRecortado";

/** jsdom no mide: simula el ancho del texto y el del contenedor. */
function simularAnchos(elemento: HTMLElement, texto: number, visible: number) {
  Object.defineProperty(elemento, "scrollWidth", { configurable: true, value: texto });
  Object.defineProperty(elemento, "clientWidth", { configurable: true, value: visible });
}

describe("TextoRecortado", () => {
  afterEach(() => {
    document.documentElement.style.removeProperty("zoom");
  });

  it("reserva un ancho mínimo para la columna y deja que el texto use el resto", () => {
    render(<TextoRecortado texto="Montenegro-Echeverría, Agustina" anchoMinimo={160} />);

    const texto = screen.getByText("Montenegro-Echeverría, Agustina");
    expect(texto).toHaveClass("adoc-texto-recortado");
    // El mínimo va en la caja: el texto no fija el ancho, ocupa el que tenga la columna.
    expect(texto.parentElement).toHaveStyle({ minWidth: "160px" });
    expect(texto).not.toHaveStyle({ maxWidth: "160px" });
  });

  it("si el texto no entra, al pasar el mouse muestra el texto completo casi al instante", async () => {
    render(<TextoRecortado texto="Montenegro-Echeverría, Agustina" anchoMinimo={160} />);
    const texto = screen.getByText("Montenegro-Echeverría, Agustina");
    simularAnchos(texto, 320, 200);

    fireEvent.mouseEnter(texto);

    const tooltip = await screen.findByRole("tooltip", {}, { timeout: 300 });
    expect(tooltip).toHaveTextContent("Montenegro-Echeverría, Agustina");

    fireEvent.mouseLeave(texto);
    expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
  });

  it("si el texto entra, no muestra un tooltip redundante", async () => {
    render(<TextoRecortado texto="Paz, Inés" anchoMinimo={160} />);
    const texto = screen.getByText("Paz, Inés");
    simularAnchos(texto, 80, 200);

    fireEvent.mouseEnter(texto);
    await new Promise((resolver) => setTimeout(resolver, 200));

    expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
  });

  it("ubica el tooltip debajo del texto compensando el zoom de la raíz", async () => {
    document.documentElement.style.setProperty("zoom", "1.125");
    render(<TextoRecortado texto="Montenegro-Echeverría, Agustina" anchoMinimo={160} />);
    const texto = screen.getByText("Montenegro-Echeverría, Agustina");
    simularAnchos(texto, 320, 200);
    texto.getBoundingClientRect = () =>
      ({ top: 450, bottom: 477, left: 225, right: 425, width: 200, height: 27 }) as DOMRect;

    fireEvent.mouseEnter(texto);

    const tooltip = await screen.findByRole("tooltip", {}, { timeout: 300 });
    // 477 / 1.125 = 424 (+6 de separación); 225 / 1.125 = 200.
    expect(tooltip).toHaveStyle({ top: "430px", left: "200px" });
  });
});
