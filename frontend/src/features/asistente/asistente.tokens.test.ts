import { describe, it, expect } from "vitest";

// Como texto y no como estilos: acá se lee la hoja, no se aplica.
import hoja from "./asistente.css?raw";
import tema from "@ars-docendi/ui/theme.css?raw";

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
