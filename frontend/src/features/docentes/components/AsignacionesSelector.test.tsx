import { useState } from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { expect, it } from "vitest";
import { AsignacionesSelector, type AsignacionRow } from "./AsignacionesSelector";

it("ofrece seis dedicaciones canónicas y conserva la histórica hasta cambiarla", () => {
  function Formulario() {
    const [rows, onChange] = useState<AsignacionRow[]>([
      {
        materiaId: "m1",
        carreraId: "c1",
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
        materiasPlan={[
          {
            id: "m1",
            codigo: "03500",
            nombre: "Software",
            carreraId: "c1",
            carreraNombre: "Informática",
          },
        ]}
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

it("elige materia y carrera con el buscador y marca como ya asignada la de otra fila", () => {
  const pares = [
    {
      id: "m1",
      codigo: "03500",
      nombre: "Software",
      carreraId: "c1",
      carreraNombre: "Informática",
    },
    { id: "m1", codigo: "03500", nombre: "Software", carreraId: "c2", carreraNombre: "Civil" },
    {
      id: "m2",
      codigo: "03620",
      nombre: "Algoritmos",
      carreraId: "c1",
      carreraNombre: "Informática",
    },
  ];
  const vacia = { materiaId: "", carreraId: "", cargo: "", horas: "", dedicacionId: "" };
  function Formulario() {
    const [rows, onChange] = useState<AsignacionRow[]>([
      { ...vacia, materiaId: "m1", carreraId: "c1" },
      vacia,
    ]);
    return (
      <AsignacionesSelector
        rows={rows}
        onChange={onChange}
        materiasPlan={pares}
        cargos={["Adjunto"]}
        dedicaciones={[]}
      />
    );
  }
  render(<Formulario />);
  expect(screen.getByRole("textbox", { name: "Materia y carrera de asignación 1" })).toHaveValue(
    "Software (03500) — Informática",
  );

  fireEvent.click(screen.getAllByRole("button", { name: "Buscar" })[0]);
  const dialogo = screen.getByRole("dialog", { name: /Buscar materia/ });
  const filas = within(dialogo).getAllByRole("row").slice(1);
  expect(filas.filter((fila) => within(fila).queryByText("Ya asignada"))).toHaveLength(2);

  fireEvent.click(within(dialogo).getByText("Algoritmos"));
  expect(screen.getByRole("textbox", { name: "Materia y carrera de asignación 2" })).toHaveValue(
    "Algoritmos (03620) — Informática",
  );
});
