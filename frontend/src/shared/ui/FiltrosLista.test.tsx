import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { FiltrosLista, type CampoFiltroFijo, type CampoFiltroOpcional } from "./FiltrosLista";

interface ValoresPrueba extends Record<string, string> {
  nombre: string;
  legajo: string;
  estado: string;
}

const INICIALES: ValoresPrueba = { nombre: "", legajo: "", estado: "todos" };

const FIJOS: CampoFiltroFijo[] = [
  { clave: "nombre", placeholder: "Filtrar por nombre…", ariaLabel: "Filtrar por nombre" },
];

const OPCIONALES: CampoFiltroOpcional[] = [
  { tipo: "texto", clave: "legajo", etiqueta: "Legajo", placeholder: "Legajo…" },
  {
    tipo: "select",
    clave: "estado",
    etiqueta: "Estado",
    valorInicial: "todos",
    opciones: [
      { value: "todos", label: "Todos" },
      { value: "activo", label: "Activo" },
    ],
  },
];

/** Wrapper con estado real, como lo usaría cualquier pantalla que adopte el filtro. */
function FiltrosDePrueba() {
  const [valores, setValores] = useState<ValoresPrueba>(INICIALES);
  return (
    <>
      <FiltrosLista fijos={FIJOS} opcionales={OPCIONALES} valores={valores} onChange={setValores} />
      <p data-testid="valores">{JSON.stringify(valores)}</p>
    </>
  );
}

describe("FiltrosLista (genérico, reutilizable)", () => {
  it("el campo fijo dispara onChange con el valor tipeado", async () => {
    const user = userEvent.setup();
    render(<FiltrosDePrueba />);

    await user.type(screen.getByLabelText("Filtrar por nombre"), "ana");

    expect(screen.getByTestId("valores")).toHaveTextContent('"nombre":"ana"');
  });

  it("el selector 'Añadir filtro' solo ofrece los campos opcionales no agregados", async () => {
    const user = userEvent.setup();
    render(<FiltrosDePrueba />);

    const selector = screen.getByLabelText("Añadir filtro");
    expect(selector).toHaveTextContent("Legajo");
    expect(selector).toHaveTextContent("Estado");

    await user.selectOptions(selector, "legajo");

    expect(screen.getByLabelText("Filtrar por legajo")).toBeInTheDocument();
    expect(screen.getByLabelText("Añadir filtro")).not.toHaveTextContent("Legajo");
    expect(screen.getByLabelText("Añadir filtro")).toHaveTextContent("Estado");
  });

  it("un filtro opcional de texto vuelve a '' al quitarlo", async () => {
    const user = userEvent.setup();
    render(<FiltrosDePrueba />);

    await user.selectOptions(screen.getByLabelText("Añadir filtro"), "legajo");
    await user.type(screen.getByLabelText("Filtrar por legajo"), "1005");
    expect(screen.getByTestId("valores")).toHaveTextContent('"legajo":"1005"');

    await user.click(screen.getByRole("button", { name: "Quitar filtro de legajo" }));

    expect(screen.queryByLabelText("Filtrar por legajo")).not.toBeInTheDocument();
    expect(screen.getByTestId("valores")).toHaveTextContent('"legajo":""');
  });

  it("un filtro opcional select vuelve a su valorInicial (no '') al quitarlo", async () => {
    const user = userEvent.setup();
    render(<FiltrosDePrueba />);

    await user.selectOptions(screen.getByLabelText("Añadir filtro"), "estado");
    await user.selectOptions(screen.getByLabelText("Filtrar por estado"), "activo");
    expect(screen.getByTestId("valores")).toHaveTextContent('"estado":"activo"');

    await user.click(screen.getByRole("button", { name: "Quitar filtro de estado" }));

    expect(screen.queryByLabelText("Filtrar por estado")).not.toBeInTheDocument();
    expect(screen.getByTestId("valores")).toHaveTextContent('"estado":"todos"');
  });

  it("cuando se agregan todos los opcionales, desaparece el selector 'Añadir filtro'", async () => {
    const user = userEvent.setup();
    render(<FiltrosDePrueba />);

    await user.selectOptions(screen.getByLabelText("Añadir filtro"), "legajo");
    await user.selectOptions(screen.getByLabelText("Añadir filtro"), "estado");

    expect(screen.queryByLabelText("Añadir filtro")).not.toBeInTheDocument();
  });
});
