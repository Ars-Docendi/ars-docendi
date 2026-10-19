import { isValidElement } from "react";
import { describe, expect, it } from "vitest";
import { routes } from "./routes";

describe("rutas de Designaciones", () => {
  it("no expone una ruta índice para /designaciones", () => {
    expect(routes.children?.some((ruta) => ruta.index === true)).toBe(false);
  });

  it("protege Comisiones con el permiso comisiones.ver", () => {
    const grupo = routes.children?.find((ruta) =>
      ruta.children?.some((hija) => hija.path === "comisiones"),
    );
    expect(grupo).toBeDefined();
    expect(isValidElement(grupo?.element) && grupo.element.props).toMatchObject({
      permission: "comisiones.ver",
    });
  });

  it("deja la edición fuera del gate exclusivo del Jefe de Cátedra", () => {
    expect(routes.children?.some((ruta) => ruta.path === "pedidos/:id/editar")).toBe(true);
  });
});
