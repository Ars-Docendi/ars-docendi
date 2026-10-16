import { useState } from "react";
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { CampoPeriodo } from "./CampoPeriodo";

function Periodo({ inicial = "" }: { inicial?: string }) {
  const [desde, setDesde] = useState(inicial);
  const [hasta, setHasta] = useState<string | null>("");
  const [enCurso, setEnCurso] = useState(false);
  return (
    <>
      <CampoPeriodo
        desde={desde}
        hasta={hasta}
        enCurso={enCurso}
        etiquetaEnCurso="Sigo en este puesto"
        onDesde={setDesde}
        onHasta={setHasta}
        onEnCurso={setEnCurso}
      />
      <output data-testid="desde">{desde}</output>
    </>
  );
}

describe("CampoPeriodo — mes y año en cualquier orden", () => {
  it("conserva el mes elegido antes que el año", async () => {
    const user = userEvent.setup();
    render(<Periodo />);

    await user.selectOptions(screen.getByLabelText("Mes de desde"), "03");
    expect(screen.getByLabelText("Mes de desde")).toHaveValue("03");

    await user.type(screen.getByLabelText("Año de desde"), "2014");
    expect(screen.getByTestId("desde")).toHaveTextContent("2014-03");
  });

  it("compone el mes elegido después del año", async () => {
    const user = userEvent.setup();
    render(<Periodo />);

    await user.type(screen.getByLabelText("Año de desde"), "2014");
    await user.selectOptions(screen.getByLabelText("Mes de desde"), "03");
    expect(screen.getByTestId("desde")).toHaveTextContent("2014-03");
  });

  it("solo año deja el valor sin mes", async () => {
    const user = userEvent.setup();
    render(<Periodo />);

    await user.type(screen.getByLabelText("Año de desde"), "2014");
    expect(screen.getByTestId("desde")).toHaveTextContent(/^2014$/);
  });

  it("corregir el año con mes elegido no corrompe el valor", async () => {
    const user = userEvent.setup();
    render(<Periodo inicial="2014-03" />);

    await user.type(screen.getByLabelText("Año de desde"), "{Backspace}5");
    expect(screen.getByTestId("desde")).toHaveTextContent("2015-03");
  });
});
