/// <reference types="node" />
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, it, expect } from "vitest";

// jsdom no calcula layout, así que «el input no se mueve con la rueda» no se
// puede afirmar montando el componente. Lo que sí se puede afirmar es la regla
// que lo produce, leyendo la hoja: es el mismo criterio con que
// `asistente.tokens.test.ts` y `TablaDeResultado.test.tsx` custodian el CSS.
//
// El import de `.css` no sirve acá: con `test.css: false` en vite.config.ts
// vitest lo resuelve a cadena vacía —también con `?raw`— y el test pasaría sobre
// la nada.
const hoja = readFileSync(join(import.meta.dirname, "asistente.css"), "utf8");

/** El cuerpo de una regla, por selector exacto. */
function reglaDe(selector: string): string {
  const abre = hoja.indexOf(`${selector} {`);
  expect(abre, `no encontré la regla \`${selector}\` en asistente.css`).toBeGreaterThan(-1);
  return hoja.slice(abre, hoja.indexOf("}", abre));
}

describe("el panel de la ruta llena su contenedor sin desbordarlo", () => {
  it("toma su alto del contenedor y no de una resta de medidas del shell", () => {
    const regla = reglaDe(".adoc-asistente-pagina");

    expect(regla).toMatch(/height:\s*100%/);

    // LA REGRESIÓN QUE ESTE TEST EXISTE PARA EVITAR. La versión anterior restaba
    // a mano la barra —56px— y el relleno de `.adoc-main` —24 y 48—. Daba el
    // número correcto el día que se escribió y se desajustaba en silencio con
    // cualquier cambio del shell; el síntoma era el campo de entrada yéndose con
    // la rueda, que nadie atribuye a una resta en otra hoja.
    expect(regla).not.toMatch(/\b56px\b/);
    expect(regla).not.toMatch(/\b48px\b/);
    expect(regla).not.toMatch(/100vh/);
  });

  it("deja que el hilo absorba la falta de espacio en una ventana baja", () => {
    // Sin `min-height: 0` en toda la cadena, el mínimo de contenido del hilo
    // infla la columna, la columna desborda `.adoc-main` y el input vuelve a
    // moverse. Los tres eslabones tienen que declararlo.
    for (const selector of [
      ".adoc-asistente-pagina",
      ".adoc-asistente-pagina .adoc-asistente",
      ".adoc-asistente-hilo",
    ]) {
      expect(reglaDe(selector), `\`${selector}\` sin min-height: 0`).toMatch(/min-height:\s*0/);
    }
  });
});
