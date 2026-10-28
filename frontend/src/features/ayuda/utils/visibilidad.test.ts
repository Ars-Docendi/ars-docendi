import { describe, expect, it } from "vitest";

import { CONTENIDO as C } from "../testing/contenidoPrueba";
import type { ContenidoAyuda } from "../tipos";
import { articuloVisible, articulosVisibles, modulosVisibles, puedeVer } from "./visibilidad";

describe("puedeVer", () => {
  it("lista vacía: visible para cualquier sesión", () => {
    expect(puedeVer([], [])).toBe(true);
    expect(puedeVer([], ["portal.ver"])).toBe(true);
  });

  it("alcanza con cualquiera de los permisos requeridos", () => {
    const requeridos = ["designaciones.revisar", "periodos.administrar"];
    expect(puedeVer(requeridos, ["periodos.administrar"])).toBe(true);
    expect(puedeVer(requeridos, ["designaciones.revisar"])).toBe(true);
  });

  it("sin ninguno de los permisos no es visible", () => {
    expect(puedeVer(["roles.ver"], ["portal.ver"])).toBe(false);
    expect(puedeVer(["roles.ver"], [])).toBe(false);
  });
});

describe("articulosVisibles", () => {
  it("sin permisos solo quedan los artículos abiertos, en orden de grupo", () => {
    expect(articulosVisibles([], C).map((a) => a.slug)).toEqual(["bienvenida", "atajos"]);
  });

  it("ordena por índice de grupo y luego por declaración", () => {
    const invertido: ContenidoAyuda = {
      modulos: C.modulos,
      articulos: [...C.articulos].reverse(),
    };
    const slugs = articulosVisibles(["tareas.ver", "tareas.gestionar"], invertido)
      .filter((a) => a.moduloSlug === "tareas")
      .map((a) => a.slug);
    expect(slugs).toEqual(["ver-tareas", "crear-tarea"]);
  });

  it("descarta artículos cuyo módulo no es visible aunque el artículo lo sea", () => {
    const contenido: ContenidoAyuda = {
      modulos: C.modulos.map((m) => (m.slug === "general" ? { ...m, permisos: ["x.ver"] } : m)),
      articulos: C.articulos,
    };
    expect(articulosVisibles([], contenido)).toEqual([]);
  });
});

describe("modulosVisibles", () => {
  it("oculta módulos sin artículos visibles aunque la persona cumpla su permiso", () => {
    // `designaciones.revisar` habilita el módulo, pero solo su artículo de revisión.
    const slugs = modulosVisibles(["designaciones.revisar"], C).map((m) => m.slug);
    expect(slugs).toEqual(["general", "designaciones"]);
  });

  it("sin permisos de módulos muestra solo los abiertos", () => {
    expect(modulosVisibles([], C).map((m) => m.slug)).toEqual(["general"]);
  });

  it("sin artículos en el contenido no hay módulos", () => {
    expect(modulosVisibles([], { modulos: C.modulos, articulos: [] })).toEqual([]);
  });
});

describe("articuloVisible", () => {
  it("devuelve el artículo cuando módulo, pertenencia y permiso coinciden", () => {
    expect(articuloVisible("tareas", "ver-tareas", ["tareas.ver"], C)?.titulo).toBe(
      "Ver tus tareas",
    );
  });

  it("devuelve undefined si falta el permiso", () => {
    expect(articuloVisible("administracion", "gestionar-roles", [], C)).toBeUndefined();
  });

  it("devuelve undefined si el artículo pertenece a otro módulo", () => {
    expect(articuloVisible("tareas", "gestionar-roles", ["roles.ver"], C)).toBeUndefined();
  });

  it("devuelve undefined si el módulo o el artículo no existen", () => {
    expect(articuloVisible("no-existe", "x", [], C)).toBeUndefined();
    expect(articuloVisible("general", "no-existe", [], C)).toBeUndefined();
  });
});
