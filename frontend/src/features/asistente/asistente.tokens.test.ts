/// <reference types="node" />
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { join } from "node:path";
import { describe, it, expect } from "vitest";

// Como texto y no como estilos: acá se lee la hoja, no se aplica. Va por `fs`
// y no por `?raw`: con `css: false` en la config, vitest resuelve cualquier
// import de un `.css` —también con `?raw`— a una cadena vacía, y este test
// pasó un tiempo afirmando cosas sobre dos cadenas vacías. El tema se resuelve
// como lo publica la librería (`exports["./theme.css"]`), no por una ruta a
// `node_modules` que un bump puede mover. Los tipos de node se referencian acá
// y no en el tsconfig de la app, que no los carga.
const hoja = readFileSync(join(import.meta.dirname, "asistente.css"), "utf8");
const tema = readFileSync(
  createRequire(import.meta.url).resolve("@ars-docendi/ui/theme.css"),
  "utf8",
);

// ============================================================
// La hoja de estilos del asistente contra el tema.
//
// Un `var(--token, fallback)` sobre un token que NO existe se renderiza siempre
// con el fallback y nadie lo nota: así fue como la superficie llegó a pintarse
// en slate e indigo ajenos al acento institucional. jsdom no aplica CSS, pero
// el contrato que importa es textual y se puede fijar acá: cada token que la
// hoja usa está definido en el tema, y ningún color se escribe a mano.
// ============================================================

const sinComentarios = hoja.replace(/\/\*[\s\S]*?\*\//g, "");

describe("La hoja de estilos del asistente", () => {
  it("se lee de verdad: ni la hoja ni el tema son cadenas vacías", () => {
    expect(hoja.length).toBeGreaterThan(0);
    expect(tema.length).toBeGreaterThan(0);
  });

  it("usa sólo tokens que el tema define", () => {
    const usados = new Set(
      [...sinComentarios.matchAll(/var\(\s*(--[\w-]+)/g)].map((coincidencia) => coincidencia[1]),
    );
    const definidosEnElTema = new Set(
      [...tema.matchAll(/(--[\w-]+)\s*:/g)].map((coincidencia) => coincidencia[1]),
    );
    // Los que la hoja define para sí misma llevan el prefijo de la feature.
    const propios = new Set(
      [...sinComentarios.matchAll(/(--adoc-asistente-[\w-]+)\s*:/g)].map(
        (coincidencia) => coincidencia[1],
      ),
    );

    const huerfanos = [...usados].filter(
      (token) => !definidosEnElTema.has(token) && !propios.has(token),
    );

    expect(huerfanos).toEqual([]);
  });

  it("no escribe ningún color a mano: los hexadecimales sólo viven en comentarios", () => {
    expect(sinComentarios).not.toMatch(/#[0-9a-fA-F]{3,8}\b/);
  });
});
