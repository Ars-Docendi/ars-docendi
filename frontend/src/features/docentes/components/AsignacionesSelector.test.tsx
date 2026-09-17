import { useState } from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { expect, it } from "vitest";
import { AsignacionesSelector, type AsignacionRow } from "./AsignacionesSelector";

it("ofrece seis dedicaciones canónicas y conserva la histórica hasta cambiarla", () => {
  function Formulario() {
    const [rows, onChange] = useState<AsignacionRow[]>([
      {
        materia: "03500",
        cargo: "Adjunto",
        horas: "12",
        dedicacionId: "",
        dedicacionLegada: "Categoría 0",
      },
    ]);
    return (
      <AsignacionesSelector
        rows={rows}
        onChange={onChange}
        materias={[{ id: "m1", codigo: "03500", nombre: "Software" }]}
        cargos={["Adjunto"]}
        dedicaciones={Array.from({ length: 6 }, (_, i) => ({
          id: `d${i + 1}`,
          nombre: `Categoría ${i + 1}`,
        }))}
      />
    );
  }
  render(<Formulario />);
  const selector = screen.getByRole("combobox", { name: "Dedicación de asignación 1" });
  expect(selector).toHaveValue("");
  expect(within(selector).getAllByRole("option")).toHaveLength(7);
  for (const numero of [1, 2, 6]) {
    fireEvent.change(selector, { target: { value: `d${numero}` } });
    expect(selector).toHaveValue(`d${numero}`);
  }
});
