import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it } from "vitest";

import { CampoFechaISO } from "./CampoFechaISO";

function Demo({ inicial = "" }: { inicial?: string }) {
  const [iso, setIso] = useState(inicial);
  return (
    <>
      <CampoFechaISO aria-label="Fecha" value={iso} onChange={setIso} />
      <output aria-label="ISO">{iso}</output>
      <button type="button" onClick={() => setIso("2027-01-31")}>
        Poner
      </button>
    </>
  );
}

describe("CampoFechaISO", () => {
  it("muestra dd/mm/aaaa y entrega ISO sólo con una fecha real y completa", async () => {
    render(<Demo />);
    const campo = screen.getByLabelText("Fecha");

    await userEvent.type(campo, "3101");
    expect(screen.getByLabelText("ISO")).toHaveTextContent("");
    await userEvent.type(campo, "2026");
    expect(campo).toHaveValue("31/01/2026");
    expect(screen.getByLabelText("ISO")).toHaveTextContent("2026-01-31");
  });

  it("una fecha imposible no llega al formulario", async () => {
    render(<Demo />);
    await userEvent.type(screen.getByLabelText("Fecha"), "31022026");
    expect(screen.getByLabelText("ISO")).toHaveTextContent("");
  });

  it("refleja un valor ISO que cambia desde afuera", async () => {
    render(<Demo inicial="2026-03-05" />);
    expect(screen.getByLabelText("Fecha")).toHaveValue("05/03/2026");
    await userEvent.click(screen.getByRole("button", { name: "Poner" }));
    expect(screen.getByLabelText("Fecha")).toHaveValue("31/01/2027");
  });
});
