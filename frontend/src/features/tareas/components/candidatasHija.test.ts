import { describe, expect, it } from "vitest";
import type { Tarea } from "../types";
import { candidatasHija } from "./candidatasHija";

const tarea = (id: string, extra: Partial<Tarea> = {}) =>
  ({ id, estado: "pendiente", ...extra }) as Tarea;

describe("candidatasHija", () => {
  it("ofrece sólo tareas sin padre, no canceladas y que no generen un ciclo", () => {
    const raiz = tarea("raiz");
    const madre = tarea("madre", { tareaPadreId: "raiz" });
    const actual = tarea("actual", { tareaPadreId: "madre" });
    const libre = tarea("libre");
    const conOtroPadre = tarea("con-padre", { tareaPadreId: "libre" });
    const cancelada = tarea("cancelada", { estado: "cancelada" });

    const resultado = candidatasHija(actual, [raiz, madre, actual, libre, conOtroPadre, cancelada]);

    expect(resultado.map((t) => t.id)).toEqual(["libre"]);
  });
});
