import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { BuscadorMaterias } from "./BuscadorMaterias";
import type { OpcionMateriaCarrera } from "./busquedaMaterias";

const opciones: OpcionMateriaCarrera[] = [
  {
    id: "m1",
    codigo: "01023",
    nombre: "ANALISIS MATEMATICO I",
    carreraId: "c1",
    carreraCodigo: "201",
    carreraNombre: "Ingeniería en Informática",
  },
  {
    id: "m2",
    codigo: "01035",
    nombre: "FISICA II",
    carreraId: "c2",
    carreraCodigo: "207",
    carreraNombre: "Ingeniería Civil",
  },
];

function renderizar(props: Partial<Parameters<typeof BuscadorMaterias>[0]> = {}) {
  const onSeleccionar = vi.fn();
  const onCerrar = vi.fn();
  render(
    <BuscadorMaterias
      open
      modo="par"
      opciones={opciones}
      onSeleccionar={onSeleccionar}
      onCerrar={onCerrar}
      {...props}
    />,
  );
  return { onSeleccionar, onCerrar };
}

function filas() {
  return within(screen.getByRole("table")).getAllByRole("row").slice(1);
}

describe("BuscadorMaterias", () => {
  it("aplica los filtros recién al buscar con Enter", () => {
    renderizar();
    const codigoCarrera = screen.getByLabelText("Código carrera");
    fireEvent.change(codigoCarrera, { target: { value: "207" } });
    expect(filas()).toHaveLength(2);

    fireEvent.submit(codigoCarrera.closest("form")!);

    expect(filas()).toHaveLength(1);
    expect(screen.getByText("1 de 2 materias")).toBeInTheDocument();
    expect(screen.getByText("FISICA II")).toBeInTheDocument();
  });

  it("elige una fila con click o teclado y cierra", () => {
    const { onSeleccionar, onCerrar } = renderizar();
    fireEvent.keyDown(filas()[0], { key: "Enter" });
    expect(onSeleccionar).toHaveBeenCalledWith(
      expect.objectContaining({ materiaId: "m1", carrera: expect.objectContaining({ id: "c1" }) }),
    );
    expect(onCerrar).toHaveBeenCalled();
  });

  it("muestra las opciones no elegibles con su motivo y no las selecciona", () => {
    const { onSeleccionar } = renderizar({
      motivoNoElegible: (r) => (r.materiaId === "m1" ? "Ya asignada" : undefined),
    });
    const fila = filas()[0];
    expect(within(fila).getByText("Ya asignada")).toBeInTheDocument();
    expect(fila).toHaveAttribute("aria-disabled", "true");
    fireEvent.click(fila);
    expect(onSeleccionar).not.toHaveBeenCalled();
  });

  it("cancelar cierra sin seleccionar", () => {
    const { onSeleccionar, onCerrar } = renderizar();
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    expect(onCerrar).toHaveBeenCalled();
    expect(onSeleccionar).not.toHaveBeenCalled();
  });

  it("en modo materia lista las carreras en que se dicta", () => {
    renderizar({ modo: "materia" });
    expect(screen.getByRole("columnheader", { name: "Se dicta en" })).toBeInTheDocument();
    expect(screen.getByText("201 · Ingeniería en Informática")).toBeInTheDocument();
  });
});
