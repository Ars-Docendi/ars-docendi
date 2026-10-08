import { describe, it, expect } from "vitest";

import { filtrarNavegacion } from "./nav";

// ============================================================
// El ítem «Sistema» (sistema-seccion-unificada, ARS-154, design D9): una sola
// entrada de navegación que alcanza con CUALQUIERA de sus tres permisos.
// Reemplaza las antiguas «Dashboard del sistema», «Registros de auditoría» y
// «Uso del asistente» (asistente-administracion-de-uso, tasks.md 11.2), que ya
// no existen como ítems propios.
//
// El resto de `filtrarNavegacion` ya se prueba de punta a punta contra
// `Sidebar` en `Sidebar.test.tsx`; este archivo cubre puntualmente el ítem
// nuevo contra la función pura, sin tener que montar todo el shell.
// ============================================================

describe("El ítem «Sistema» (any-of sistema.estado.ver | asistente.administrar | auditoria.ver)", () => {
  it.each([["sistema.estado.ver"], ["asistente.administrar"], ["auditoria.ver"]])(
    "aparece con sólo %s",
    (permiso) => {
      const grupos = filtrarNavegacion([permiso]);
      const items = grupos.flatMap((g) => g.items);

      const item = items.find((i) => i.label === "Sistema");
      expect(item).toBeDefined();
      expect(item?.to).toBe("/sistema");
    },
  );

  it("no aparece sin ninguno de los tres permisos", () => {
    const grupos = filtrarNavegacion(["portal.ver"]);
    const items = grupos.flatMap((g) => g.items);

    expect(items.some((item) => item.label === "Sistema")).toBe(false);
  });

  it("ya no existen ítems separados para el dashboard, la auditoría o el uso del asistente", () => {
    const grupos = filtrarNavegacion([
      "sistema.estado.ver",
      "asistente.administrar",
      "auditoria.ver",
    ]);
    const items = grupos.flatMap((g) => g.items);

    expect(items.filter((item) => item.label === "Sistema")).toHaveLength(1);
    expect(items.some((item) => item.label === "Dashboard del sistema")).toBe(false);
    expect(items.some((item) => item.label === "Registros de auditoría")).toBe(false);
    expect(items.some((item) => item.label === "Uso del asistente")).toBe(false);
  });
});
