import { afterEach, describe, expect, it, vi } from "vitest";

import { aplicarTema, iniciarTema } from "./tema";

/** `matchMedia` falso cuyo modo oscuro se puede cambiar en vivo. */
function dispositivo(oscuro: boolean) {
  let alCambiar = () => {};
  const consulta = {
    get matches() {
      return oscuro;
    },
    addEventListener: (_: string, callback: () => void) => (alCambiar = callback),
  };
  vi.stubGlobal("matchMedia", () => consulta);
  return (nuevo: boolean) => {
    oscuro = nuevo;
    alCambiar();
  };
}

afterEach(() => vi.unstubAllGlobals());

describe("tema del sistema", () => {
  it("aplicarTema('sistema') respeta el modo del dispositivo", () => {
    dispositivo(true);
    aplicarTema("sistema");
    expect(document.documentElement.dataset.theme).toBe("dark");

    dispositivo(false);
    aplicarTema("sistema");
    expect(document.documentElement.dataset.theme).toBe("light");
  });

  it("iniciarTema vuelve a aplicar el tema cuando el dispositivo cambia", () => {
    const cambiar = dispositivo(false);
    iniciarTema();
    expect(document.documentElement.dataset.theme).toBe("light");

    cambiar(true);
    expect(document.documentElement.dataset.theme).toBe("dark");
  });

  it("una elección explícita no la pisa el cambio del dispositivo", () => {
    localStorage.setItem("ars-docendi:tema", "claro");
    const cambiar = dispositivo(false);
    iniciarTema();
    cambiar(true);
    expect(document.documentElement.dataset.theme).toBe("light");
  });
});
