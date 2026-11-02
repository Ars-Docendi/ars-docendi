/// <reference types="node" />
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

// Con `css: false` Vitest vacía los imports de CSS: se lee del disco.
const leer = (ruta: string) => readFileSync(new URL(ruta, import.meta.url), "utf8");

describe("botonera del formulario de pedido", () => {
  it("se acomoda en filas: en 360 px «Cancelar» no se sale por la izquierda de la tarjeta", () => {
    expect(leer("./pedidoForm.css")).toMatch(/\.adoc-pf-actions\s*\{[^}]*flex-wrap:\s*wrap/);
  });
});
