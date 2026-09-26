import { describe, it, expect } from "vitest";

import { filtrarNavegacion } from "./nav";

// ============================================================
// El ítem de navegación de administración del asistente
// (asistente-administracion-de-uso, tasks.md 11.2).
//
// El resto de `filtrarNavegacion` ya se prueba de punta a punta contra
// `Sidebar` en `Sidebar.test.tsx`; este archivo cubre puntualmente el ítem
// nuevo contra la función pura, sin tener que montar todo el shell.
// ============================================================

describe("El ítem «Uso del asistente» (asistente.administrar)", () => {
  it("no aparece para un actor sin el permiso", () => {
    const grupos = filtrarNavegacion(["asistente.leer_historial_ajeno"]);
    const items = grupos.flatMap((g) => g.items);

    expect(items.some((item) => item.label === "Uso del asistente")).toBe(false);
  });

  it("aparece para un actor con `asistente.administrar`", () => {
    const grupos = filtrarNavegacion(["asistente.administrar"]);
    const items = grupos.flatMap((g) => g.items);

    const item = items.find((i) => i.label === "Uso del asistente");
    expect(item).toBeDefined();
    expect(item?.to).toBe("/asistente/administracion");
  });
});
