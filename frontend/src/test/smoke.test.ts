// Smoke test del harness: confirma que Vitest + jsdom + globals + jest-dom
// están operativos. Se puede borrar una vez que existan tests reales.
import { describe, it, expect } from "vitest";

describe("harness de tests", () => {
  it("corre Vitest con globals", () => {
    expect(1 + 1).toBe(2);
  });

  it("tiene jsdom disponible", () => {
    const div = document.createElement("div");
    div.textContent = "ars-docendi";
    expect(div).toHaveTextContent("ars-docendi");
  });

  it("aísla localStorage entre tests", () => {
    expect(localStorage.getItem("adoc.mock.pedidos")).toBeNull();
  });
});
