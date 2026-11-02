/// <reference types="node" />
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";

import { MarcaArsDocendi } from "../../app/shell/MarcaArsDocendi";
import { PageHeader } from "../ui/PageHeader";
import { Migas } from "./Migas";

// Con `css: false` Vitest vacía los imports de CSS: se leen del disco, en el orden en que la app los
// carga, para que la especificidad y el orden decidan como en el navegador.
const aca = dirname(fileURLToPath(import.meta.url));
const HOJAS = [
  createRequire(import.meta.url).resolve("@ars-docendi/ui/components.css"),
  join(aca, "navegacion.css"),
  join(aca, "../../app/shell/shell.css"),
];
const ANCHO_TELEFONO = 360;

/** `true` si la condición `(max-width: Npx)` / `(min-width: Npx)` vale para `ancho`. */
function aplica(condicion: string, ancho: number) {
  // shortcut: sólo max-width/min-width en px, lo único que usan estas hojas; ampliar si aparece otra.
  const partes = [...condicion.matchAll(/\((max|min)-width:\s*([\d.]+)px\)/g)];
  return (
    partes.length > 0 &&
    partes.every(([, tipo, px]) => (tipo === "max" ? ancho <= Number(px) : ancho >= Number(px)))
  );
}

/**
 * jsdom no evalúa `@media`: cada regla que vale para `ancho` se reemplaza por sus reglas internas
 * en el mismo lugar de la hoja, así conservan su posición en la cascada.
 */
function montarHojas(ancho: number) {
  for (const ruta of HOJAS) {
    const estilo = document.createElement("style");
    estilo.textContent = readFileSync(ruta, "utf8");
    document.head.append(estilo);
    const hoja = estilo.sheet!;
    for (let i = hoja.cssRules.length - 1; i >= 0; i--) {
      const regla = hoja.cssRules[i];
      if (!(regla instanceof CSSMediaRule)) continue;
      const internas = aplica(regla.conditionText, ancho)
        ? [...regla.cssRules].map((r) => r.cssText)
        : [];
      hoja.deleteRule(i);
      internas.forEach((texto, k) => hoja.insertRule(texto, i + k));
    }
  }
}

afterEach(() => document.head.querySelectorAll("style").forEach((e) => e.remove()));

describe("pantallas angostas", () => {
  it("el encabezado de página apila título y acciones a lo ancho, pese a la base de shell.css", () => {
    montarHojas(ANCHO_TELEFONO);
    render(
      <main className="adoc-main">
        <PageHeader title="Pedidos" actions={<button type="button">Nuevo</button>} />
      </main>,
    );
    const encabezado = getComputedStyle(screen.getByRole("heading").closest(".adoc-page-head")!);
    expect(encabezado.flexDirection).toBe("column");
    expect(encabezado.alignItems).toBe("stretch");
  });

  it("en pantalla ancha el encabezado conserva la alineación de shell.css", () => {
    montarHojas(1280);
    render(
      <main className="adoc-main">
        <PageHeader title="Pedidos" />
      </main>,
    );
    const encabezado = getComputedStyle(screen.getByRole("heading").closest(".adoc-page-head")!);
    expect(encabezado.alignItems).toBe("flex-end");
  });

  it("la marca y las migas llegan a 24 px de alto", () => {
    montarHojas(ANCHO_TELEFONO);
    render(
      <MemoryRouter>
        <header className="adoc-topbar">
          <MarcaArsDocendi />
        </header>
        <Migas items={[{ label: "Inicio", href: "/" }, { label: "Pedidos" }]} />
      </MemoryRouter>,
    );
    expect(getComputedStyle(screen.getByRole("link", { name: /Ars Docendi/ })).minHeight).toBe(
      "24px",
    );
    expect(getComputedStyle(screen.getByRole("link", { name: "Inicio" })).minHeight).toBe("24px");
  });
});
