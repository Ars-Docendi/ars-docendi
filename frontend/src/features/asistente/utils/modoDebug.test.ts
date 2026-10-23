import { describe, expect, it } from "vitest";

import { resolverModoDebugAsistente } from "./modoDebug";

// Resolver puro, sin `import.meta.env`: a diferencia de
// `resolverDevelopmentAuthEnabled`, acá NO hay fallback a modo desarrollo — el
// razonamiento crudo no debe verse por el solo hecho de correr `vite dev`.
describe("resolverModoDebugAsistente", () => {
  it("se habilita sólo con el opt-in explícito", () => {
    expect(resolverModoDebugAsistente({ configuredValue: "true" })).toBe(true);
  });

  it("queda apagado sin la variable configurada", () => {
    expect(resolverModoDebugAsistente({})).toBe(false);
  });

  it("queda apagado con cualquier otro valor", () => {
    expect(resolverModoDebugAsistente({ configuredValue: "false" })).toBe(false);
    expect(resolverModoDebugAsistente({ configuredValue: "1" })).toBe(false);
  });
});
