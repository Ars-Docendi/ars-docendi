import { describe, expect, it } from "vitest";
import { routes } from "./routes";

describe("rutas de Designaciones", () => {
  it("no expone una ruta índice para /designaciones", () => {
    expect(routes.children?.some((ruta) => ruta.index === true)).toBe(false);
  });

  it("deja la edición fuera del gate exclusivo del Jefe de Cátedra", () => {
    expect(routes.children?.some((ruta) => ruta.path === "pedidos/:id/editar")).toBe(true);
  });
});
