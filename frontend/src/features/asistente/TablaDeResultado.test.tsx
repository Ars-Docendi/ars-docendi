import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";

import { TablaDeResultado } from "./components/TablaDeResultado";
import { montar } from "./test/soporte";
import type { ColumnaDelResultado } from "./types";

// ============================================================
// La tabla de resultados: su marco y la marca de columna sensible.
//
// jsdom no calcula layout, así que el scroll dentro del marco no se puede afirmar
// acá; lo que sí se fija es el contrato mínimo del que depende: la clase propia
// llega al envoltorio de la librería. Si un bump de @ars-docendi/ui deja de
// aplicar `className`, este test cae antes de que alguien note que la tabla
// volvió a recortarse.
// ============================================================

const COLUMNAS: ColumnaDelResultado[] = [
  { nombre: "apellido", sensible: false },
  { nombre: "documento", sensible: true },
  { nombre: "horas", sensible: false },
];

const FILAS: unknown[][] = [["Gómez", "28341567", 42]];

describe("El marco de la tabla", () => {
  it("el envoltorio de la librería lleva la clase propia que sobreescribe el recorte", () => {
    const { container } = montar(
      <TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} />,
    );

    expect(container.querySelector(".adoc-table-wrap.adoc-asistente-tabla-wrap")).not.toBeNull();
  });

  it("las celdas con números usan la variante numérica de la librería", () => {
    montar(<TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} />);

    expect(screen.getByText("42").closest("td")).toHaveClass("num");
    expect(screen.getByText("Gómez").closest("td")).not.toHaveClass("num");
  });
});

describe("La columna sensible", () => {
  it("se marca y se anuncia; la que no lo es, no", () => {
    montar(<TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} />);

    // El candado es para quien ve; «(dato personal)» para quien escucha. Las dos
    // cosas en la misma cabecera, para que el lector de pantalla lo diga al pasar
    // por la columna y no haya que buscar una leyenda aparte.
    expect(
      screen.getByRole("columnheader", { name: /documento.*dato personal/i }),
    ).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /apellido/i })).not.toHaveAccessibleName(
      /dato personal/i,
    );

    expect(screen.getByText(/Las columnas con candado contienen datos personales/)).toBeVisible();
  });

  it("sin columnas sensibles no hay leyenda", () => {
    montar(
      <TablaDeResultado
        columnas={COLUMNAS.map((columna) => ({ ...columna, sensible: false }))}
        filas={FILAS}
        truncado={false}
      />,
    );

    expect(screen.queryByText(/columnas con candado/)).toBeNull();
    expect(screen.queryByText(/dato personal/)).toBeNull();
  });
});
