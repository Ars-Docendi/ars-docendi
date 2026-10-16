import { describe, it, expect } from "vitest";
import { estadosDePreset, PRESET_INICIAL } from "./presetEstado";

describe("estadosDePreset", () => {
  it("Pendientes selecciona los estados no terminales", () => {
    expect(estadosDePreset("pendientes")).toEqual(["pendiente", "en_curso", "pausa"]);
  });

  it("Terminadas selecciona los estados terminales", () => {
    expect(estadosDePreset("terminadas")).toEqual(["resuelta", "cancelada"]);
  });

  it("Todas no acota por estado", () => {
    expect(estadosDePreset("todas")).toEqual([]);
  });

  it("el listado abre en Pendientes", () => {
    expect(PRESET_INICIAL).toBe("pendientes");
  });

  it("devuelve una copia: modificarla no altera el preset", () => {
    estadosDePreset("pendientes").push("resuelta");
    expect(estadosDePreset("pendientes")).toHaveLength(3);
  });
});
