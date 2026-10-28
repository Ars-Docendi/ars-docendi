import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { VisorImagen } from "./VisorImagen";

const captura = {
  src: "/ayuda/tareas/crear-una-tarea-paso-2.webp",
  alt: "Formulario de nueva tarea",
  leyenda: "Así se ve el formulario",
};

describe("VisorImagen", () => {
  it("muestra la captura inline con su alt y su leyenda, sin visor abierto", () => {
    render(<VisorImagen captura={captura} />);

    expect(screen.getByRole("img", { name: captura.alt })).toHaveAttribute("src", captura.src);
    expect(screen.getByText(captura.leyenda)).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("se abre con clic y se cierra con Esc devolviendo el foco a la captura", async () => {
    const user = userEvent.setup();
    render(<VisorImagen captura={captura} />);
    const disparador = screen.getByRole("button", { name: /Ampliar captura/ });

    await user.click(disparador);
    const visor = screen.getByRole("dialog", { name: captura.alt });
    expect(visor).toBeInTheDocument();

    await user.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(disparador).toHaveFocus();
  });

  it("se abre con el teclado", async () => {
    const user = userEvent.setup();
    render(<VisorImagen captura={captura} />);

    await user.tab();
    await user.keyboard("{Enter}");

    expect(screen.getByRole("dialog", { name: captura.alt })).toBeInTheDocument();
  });

  it("se cierra al hacer clic en el fondo y no al hacerlo sobre la imagen", async () => {
    const user = userEvent.setup();
    render(<VisorImagen captura={captura} />);
    const disparador = screen.getByRole("button", { name: /Ampliar captura/ });
    await user.click(disparador);

    const visor = screen.getByRole("dialog");
    await user.click(visor.querySelector("img")!);
    expect(screen.getByRole("dialog")).toBeInTheDocument();

    await user.click(visor.parentElement!);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(disparador).toHaveFocus();
  });

  it("al cerrar con el botón del visor también devuelve el foco", async () => {
    const user = userEvent.setup();
    render(<VisorImagen captura={captura} />);
    const disparador = screen.getByRole("button", { name: /Ampliar captura/ });
    await user.click(disparador);

    await user.click(screen.getByRole("button", { name: "Cerrar" }));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(disparador).toHaveFocus();
  });
});
