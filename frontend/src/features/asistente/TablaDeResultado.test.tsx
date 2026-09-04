/// <reference types="node" />
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";

import { TablaDeResultado } from "./components/TablaDeResultado";
import { montar } from "./test/soporte";
import type { ColumnaDelResultado } from "./types";

// La hoja como texto: jsdom no aplica CSS, pero la regla se puede leer. Va por
// `fs` y no por `?raw`: con `css: false` en la config, vitest resuelve cualquier
// import de un `.css` —también con `?raw`— a una cadena vacía. Los tipos de node
// se referencian acá y no en el tsconfig de la app, que no los carga.
const hoja = readFileSync(join(import.meta.dirname, "asistente.css"), "utf8");

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

  it("las celdas numéricas se alinean a la derecha en la hoja del asistente", () => {
    // `numeric` de la librería sólo cambia la tipografía y deja `text-align: start`:
    // una columna de cantidades quedaba pegada a la izquierda. La alineación es
    // de la hoja, así que se fija como texto: la regla sobre `td.num` dentro del
    // marco propio, sin `!important`.
    const sinComentarios = hoja.replace(/\/\*[\s\S]*?\*\//g, "");
    const regla = sinComentarios.match(/\.adoc-asistente-tabla\s+td\.num\s*\{([^}]*)\}/);

    expect(regla).not.toBeNull();
    expect(regla?.[1]).toMatch(/text-align:\s*end;/);
    expect(regla?.[1]).not.toMatch(/!important/);
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
