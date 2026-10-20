import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { propsFilaClickeable } from "./filaClickeable";

function Fila({ accion }: { accion?: () => void }) {
  return (
    <table>
      <tbody>
        <tr data-testid="fila" {...propsFilaClickeable(accion)}>
          <td>40444013</td>
          <td>
            <button type="button">Eliminar</button>
            <a href="#usuario">Ver usuario</a>
          </td>
        </tr>
      </tbody>
    </table>
  );
}

describe("propsFilaClickeable", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("ejecuta la acción al hacer click en la fila y la marca como clickeable", async () => {
    const accion = vi.fn();
    render(<Fila accion={accion} />);

    await userEvent.click(screen.getByText("40444013"));

    expect(accion).toHaveBeenCalledOnce();
    expect(screen.getByTestId("fila")).toHaveClass("adoc-fila-clickeable");
  });

  it("no la ejecuta cuando el click nace en un botón o un link de la fila", async () => {
    const accion = vi.fn();
    render(<Fila accion={accion} />);

    await userEvent.click(screen.getByRole("button", { name: "Eliminar" }));
    await userEvent.click(screen.getByRole("link", { name: "Ver usuario" }));

    expect(accion).not.toHaveBeenCalled();
  });

  it("no la ejecuta si el usuario seleccionó texto", async () => {
    const accion = vi.fn();
    vi.spyOn(window, "getSelection").mockReturnValue({
      toString: () => "40444013",
    } as Selection);
    render(<Fila accion={accion} />);

    await userEvent.click(screen.getByText("40444013"));

    expect(accion).not.toHaveBeenCalled();
  });

  it("sin acción la fila no es clickeable", async () => {
    render(<Fila />);

    expect(screen.getByTestId("fila")).not.toHaveClass("adoc-fila-clickeable");
  });

  it("la fila se enfoca con Tab y ejecuta la acción con Enter o Espacio", async () => {
    const user = userEvent.setup();
    const accion = vi.fn();
    render(<Fila accion={accion} />);

    await user.tab();
    expect(screen.getByTestId("fila")).toHaveFocus();
    await user.keyboard("{Enter}");
    await user.keyboard(" ");

    expect(accion).toHaveBeenCalledTimes(2);
  });

  it("Enter sobre un botón de la fila no ejecuta la acción de la fila", async () => {
    const user = userEvent.setup();
    const accion = vi.fn();
    render(<Fila accion={accion} />);

    screen.getByRole("button", { name: "Eliminar" }).focus();
    await user.keyboard("{Enter}");

    expect(accion).not.toHaveBeenCalled();
  });
});
