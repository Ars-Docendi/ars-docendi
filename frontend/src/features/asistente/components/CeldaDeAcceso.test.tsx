import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { CeldaDeAcceso } from "./CeldaDeAcceso";

describe("CeldaDeAcceso", () => {
  it("si el cambio falla, lo anuncia y vuelve a habilitar el interruptor", async () => {
    const user = userEvent.setup();

    render(
      <CeldaDeAcceso
        nombre="docente"
        acceso
        onCambiar={vi.fn().mockRejectedValue(new Error("500"))}
      />,
    );

    const interruptor = screen.getByRole("switch", { name: "Acceso de docente al asistente" });
    await user.click(interruptor);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "No se pudo cambiar el acceso de docente.",
    );
    expect(interruptor).toBeEnabled();
  });
});
