import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { SwitchVista } from "./SwitchVista";

describe("SwitchVista (segmented Tablero | Tabla)", () => {
  it("marca como activa la vista seleccionada", () => {
    render(<SwitchVista vista="tablero" onCambiar={vi.fn()} />);
    expect(screen.getByRole("button", { name: "Tablero" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Tabla" })).toHaveAttribute("aria-pressed", "false");
  });

  it("emite la vista elegida al hacer click", async () => {
    const user = userEvent.setup();
    const onCambiar = vi.fn();
    render(<SwitchVista vista="tablero" onCambiar={onCambiar} />);

    await user.click(screen.getByRole("button", { name: "Tabla" }));
    expect(onCambiar).toHaveBeenCalledWith("tabla");
  });
});
