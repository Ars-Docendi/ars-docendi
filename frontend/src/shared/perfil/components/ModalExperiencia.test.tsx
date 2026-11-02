import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { ModalExperiencia } from "./ModalExperiencia";

async function completarObligatorios(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByRole("textbox", { name: /Puesto/ }), "Ayudante");
  await user.type(screen.getByRole("textbox", { name: /Organización/ }), "UNLaM");
  await user.type(screen.getByRole("textbox", { name: /De qué se trató/ }), "Prácticas");
}

describe("ModalExperiencia — mes sin año", () => {
  it("bloquea el guardado si Hasta tiene mes y no tiene año", async () => {
    const user = userEvent.setup();
    const onGuardar = vi.fn();
    render(<ModalExperiencia experiencia={null} onCerrar={vi.fn()} onGuardar={onGuardar} />);

    await completarObligatorios(user);
    await user.type(screen.getByLabelText("Año de desde"), "2014");
    await user.selectOptions(screen.getByLabelText("Mes de hasta"), "06");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    expect(onGuardar).not.toHaveBeenCalled();
    expect(screen.getByText("Completá el año.")).toBeInTheDocument();
  });

  it("guarda cuando el mes se eligió antes que el año", async () => {
    const user = userEvent.setup();
    const onGuardar = vi.fn();
    render(<ModalExperiencia experiencia={null} onCerrar={vi.fn()} onGuardar={onGuardar} />);

    await completarObligatorios(user);
    await user.selectOptions(screen.getByLabelText("Mes de desde"), "03");
    await user.type(screen.getByLabelText("Año de desde"), "2014");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    expect(onGuardar).toHaveBeenCalledWith(expect.objectContaining({ desde: "2014-03" }));
  });
});
