import { useRef, useState } from "react";
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

import { useDescartarAlClicAfuera } from "./useDescartarAlClicAfuera";

function Popover() {
  const [abierto, setAbierto] = useState(false);
  const contenedor = useRef<HTMLDivElement>(null);

  useDescartarAlClicAfuera(abierto, contenedor, () => setAbierto(false));

  return (
    <div>
      <div ref={contenedor}>
        <button type="button" onClick={() => setAbierto((estaba) => !estaba)}>
          abrir
        </button>
        {abierto && <p>contenido</p>}
      </div>
      <button type="button">afuera</button>
    </div>
  );
}

const abrir = () => fireEvent.click(screen.getByRole("button", { name: "abrir" }));

describe("useDescartarAlClicAfuera", () => {
  it("cierra con un clic afuera del contenedor", () => {
    // ESTA RAMA NO ESTABA PROBADA EN NINGÚN LADO del repo, y estaba copiada en
    // cuatro popovers. Un `mousedown` que no cierra deja el menú abierto encima
    // de la pantalla siguiente, y eso no lo detecta ningún otro test.
    render(<Popover />);
    abrir();
    expect(screen.getByText("contenido")).toBeInTheDocument();

    fireEvent.mouseDown(screen.getByRole("button", { name: "afuera" }));

    expect(screen.queryByText("contenido")).not.toBeInTheDocument();
  });

  it("no cierra con un clic adentro del contenedor", () => {
    // La otra mitad, y hace falta: un hook que cerrara SIEMPRE también pasaría
    // el test de arriba, y volvería inusable cualquier popover con contenido
    // clicable adentro.
    render(<Popover />);
    abrir();

    fireEvent.mouseDown(screen.getByText("contenido"));

    expect(screen.getByText("contenido")).toBeInTheDocument();
  });

  it("cierra con Escape", () => {
    render(<Popover />);
    abrir();

    fireEvent.keyDown(document, { key: "Escape" });

    expect(screen.queryByText("contenido")).not.toBeInTheDocument();
  });

  it("no escucha mientras está cerrado", () => {
    // Sin la guarda de `abierto`, cada popover cerrado de la pantalla escucharía
    // todos los clics del documento. Se verifica por su efecto observable: con el
    // popover cerrado, un mousedown afuera no rompe nada ni lo abre.
    render(<Popover />);

    fireEvent.mouseDown(screen.getByRole("button", { name: "afuera" }));

    expect(screen.queryByText("contenido")).not.toBeInTheDocument();
  });
});
