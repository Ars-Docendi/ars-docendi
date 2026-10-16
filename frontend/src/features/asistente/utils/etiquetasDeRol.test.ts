import { describe, expect, it } from "vitest";

import { etiquetaDeRol, subtituloDeRoles } from "./etiquetasDeRol";

describe("etiquetaDeRol", () => {
  it("traduce cada código del catálogo de siete roles de sistema", () => {
    expect(etiquetaDeRol("docente")).toBe("Docente");
    expect(etiquetaDeRol("jefe_catedra")).toBe("Jefe de Cátedra");
    expect(etiquetaDeRol("coordinador_carrera")).toBe("Coordinador de Carrera");
    expect(etiquetaDeRol("secretaria")).toBe("Secretaría Académica");
    expect(etiquetaDeRol("decanato")).toBe("Decanato");
    expect(etiquetaDeRol("administrativo")).toBe("Administrativo");
    expect(etiquetaDeRol("sys_admin")).toBe("Administrador de Sistemas");
  });

  it("un código fuera del catálogo se muestra tal cual, nunca vacío", () => {
    expect(etiquetaDeRol("rol_inventado")).toBe("rol_inventado");
  });
});

describe("subtituloDeRoles", () => {
  it("une los nombres legibles de varios roles con « · »", () => {
    expect(subtituloDeRoles(["decanato", "secretaria"])).toBe("Decanato · Secretaría Académica");
  });

  it("un solo rol no lleva separador", () => {
    expect(subtituloDeRoles(["docente"])).toBe("Docente");
  });

  it("sin ningún rol no hay subtítulo que mostrar", () => {
    expect(subtituloDeRoles([])).toBeUndefined();
  });
});
