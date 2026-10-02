import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

import { FiltroEncabezado } from "./FiltroEncabezado";

function Prueba() {
  return (
    <div>
      <button type="button">Ordenar</button>
      <FiltroEncabezado etiqueta="Estado" activo onLimpiar={vi.fn()}>
        <input aria-label="Buscar estado" />
      </FiltroEncabezado>
    </div>
  );
}

describe("FiltroEncabezado", () => {
  it("abre con teclado, informa su estado y devuelve el foco al cerrar", async () => {
    const user = userEvent.setup();
    render(<Prueba />);
    const disparador = screen.getByRole("button", { name: "Filtrar Estado" });

    disparador.focus();
    await user.keyboard("{Enter}");

    expect(disparador).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("dialog", { name: "Filtro de Estado" })).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Buscar estado" })).toHaveFocus();

    await user.keyboard("{Escape}");

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(disparador).toHaveFocus();
  });

  it("cierra al hacer clic fuera sin activar el control vecino", async () => {
    const user = userEvent.setup();
    const ordenar = vi.fn();
    render(
      <div>
        <button type="button" onClick={ordenar}>
          Ordenar
        </button>
        <FiltroEncabezado etiqueta="Estado" activo={false} onLimpiar={vi.fn()}>
          <input aria-label="Buscar estado" />
        </FiltroEncabezado>
      </div>,
    );
    await user.click(screen.getByRole("button", { name: "Filtrar Estado" }));
    await user.click(screen.getByRole("button", { name: "Ordenar" }));

    expect(ordenar).toHaveBeenCalledOnce();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("ubica el menú inmediatamente debajo del control", async () => {
    const user = userEvent.setup();
    render(<Prueba />);
    const disparador = screen.getByRole("button", { name: "Filtrar Estado" });

    vi.spyOn(disparador, "getBoundingClientRect").mockReturnValue({
      top: 100,
      bottom: 124,
      left: 40,
      right: 64,
      width: 24,
      height: 24,
      x: 40,
      y: 100,
      toJSON: () => ({}),
    });

    await user.click(disparador);

    expect(screen.getByRole("dialog")).toHaveStyle({ top: "126px", left: "40px" });
  });
});
