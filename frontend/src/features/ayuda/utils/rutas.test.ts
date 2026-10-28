import { describe, expect, it } from "vitest";

import { SLUG_RESERVADO, idPaso, rutaArticulo, rutaBusqueda, rutaModulo, rutaPaso } from "./rutas";

describe("rutas de ayuda", () => {
  it("construye las URL de módulo, artículo y paso", () => {
    expect(rutaModulo("tareas")).toBe("/ayuda/tareas");
    expect(rutaArticulo("tareas", "crear-una-tarea")).toBe("/ayuda/tareas/crear-una-tarea");
    expect(idPaso(3)).toBe("paso-3");
    expect(rutaPaso("tareas", "crear-una-tarea", 3)).toBe("/ayuda/tareas/crear-una-tarea#paso-3");
  });

  it("codifica la consulta y reserva `buscar`", () => {
    expect(SLUG_RESERVADO).toBe("buscar");
    expect(rutaBusqueda("período & año")).toBe("/ayuda/buscar?q=per%C3%ADodo%20%26%20a%C3%B1o");
  });
});
