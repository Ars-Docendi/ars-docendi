/// <reference types="node" />
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

// Con `css: false` Vitest vacía los imports de CSS (incluso `?raw`): se lee del disco.
const leer = (ruta: string) => readFileSync(new URL(ruta, import.meta.url), "utf8");

describe("encabezado de página en pantallas angostas", () => {
  it("la regla móvil no queda debajo de la base de shell.css", () => {
    // shell.css (cargado después) fija `.adoc-page-head { align-items: flex-end }`. Con una
    // especificidad igual la regla móvil pierde y el título queda pegado a la derecha.
    const base = /(^|\n)\.adoc-page-head\s*\{[^}]*align-items:\s*flex-end/.test(
      leer("../../app/shell/shell.css"),
    );
    const movil = leer("./navegacion.css").split("@media (max-width: 559.98px)").join("\n");
    expect(base).toBe(true);
    expect(movil).toMatch(/\.adoc-main \.adoc-page-head\s*\{[^}]*align-items:\s*stretch/);
  });
});
