import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CampoFecha } from "./CampoFecha";
import { CampoHora } from "./CampoHora";

function Fecha() {
  const [valor, setValor] = useState("");
  return <CampoFecha id="dia" value={valor} onChange={setValor} />;
}

describe("CampoFecha", () => {
  it("masks what is typed as dd/mm/aaaa", async () => {
    const usuario = userEvent.setup();
    render(<Fecha />);
    const campo = screen.getByPlaceholderText("dd/mm/aaaa");

    await usuario.type(campo, "25122026");

    expect(campo).toHaveValue("25/12/2026");
    expect(campo).toHaveAttribute("type", "text");
  });

  it("writes the date chosen in the native calendar as dd/mm/aaaa", () => {
    render(<Fecha />);
    const nativo = document.getElementById("dia-calendario") as HTMLInputElement;

    fireEvent.change(nativo, { target: { value: "2026-11-20" } });

    expect(screen.getByPlaceholderText("dd/mm/aaaa")).toHaveValue("20/11/2026");
  });

  it("opens the native picker with the current date and survives a browser without showPicker", async () => {
    const usuario = userEvent.setup();
    const showPicker = vi.fn();
    Object.defineProperty(HTMLInputElement.prototype, "showPicker", {
      configurable: true,
      value: showPicker,
    });
    render(<Fecha />);
    await usuario.type(screen.getByPlaceholderText("dd/mm/aaaa"), "20112026");

    await usuario.click(screen.getByRole("button", { name: "Abrir calendario" }));
    expect(showPicker).toHaveBeenCalledOnce();
    expect((document.getElementById("dia-calendario") as HTMLInputElement).value).toBe(
      "2026-11-20",
    );

    showPicker.mockImplementation(() => {
      throw new DOMException("sin gesto", "NotAllowedError");
    });
    await usuario.click(screen.getByRole("button", { name: "Abrir calendario" }));
    expect(screen.getByPlaceholderText("dd/mm/aaaa")).toHaveFocus();
  });
});

describe("CampoHora", () => {
  it("masks what is typed as hh:mm", async () => {
    const usuario = userEvent.setup();
    function Hora() {
      const [valor, setValor] = useState("");
      return <CampoHora value={valor} onChange={setValor} />;
    }
    render(<Hora />);
    const campo = screen.getByPlaceholderText("hh:mm");

    await usuario.type(campo, "0930");

    expect(campo).toHaveValue("09:30");
  });
});
