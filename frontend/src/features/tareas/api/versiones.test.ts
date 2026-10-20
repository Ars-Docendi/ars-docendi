import { beforeEach, expect, it, vi } from "vitest";
import { editarTarea, cambiarEstadoTarea, editarAvance } from "./tareasApi";
import { editarProyecto, cambiarEstadoProyecto } from "./proyectosApi";
import type { DatosEditablesTarea, DatosEditablesProyecto } from "../types";

const cliente = vi.hoisted(() => ({ put: vi.fn(), post: vi.fn(), patch: vi.fn() }));
vi.mock("../../../shared/api/client", () => ({ apiClient: cliente }));
beforeEach(() => {
  vi.clearAllMocks();
  for (const metodo of Object.values(cliente)) metodo.mockResolvedValue({ data: {} });
});
it("un conflicto pide recargar y no reintenta una intención obsoleta", async () => {
  cliente.patch.mockRejectedValueOnce({ isAxiosError: true, response: { status: 409 } });
  await expect(editarAvance("t", 50, 123)).rejects.toThrow("Recargá");
  expect(cliente.patch).toHaveBeenCalledTimes(1);
});

it("envía la versión observada en cada mutación existente", async () => {
  const tarea = { titulo: "T", responsableId: "u" } as DatosEditablesTarea;
  const proyecto = { nombre: "P", responsableId: "u" } as DatosEditablesProyecto;
  await editarTarea("t", tarea, 123);
  expect(cliente.put).toHaveBeenLastCalledWith("/api/tareas/t", { ...tarea, version: 123 });
  await cambiarEstadoTarea("t", "cancelada", { version: 123 });
  expect(cliente.post).toHaveBeenLastCalledWith("/api/tareas/t/estado", {
    estado: "cancelada",
    version: 123,
  });
  await editarAvance("t", 50, 123);
  expect(cliente.patch).toHaveBeenLastCalledWith("/api/tareas/t/avance", {
    porcentajeAvance: 50,
    version: 123,
  });
  await editarProyecto("p", proyecto, 123);
  expect(cliente.put).toHaveBeenLastCalledWith("/api/tareas/proyectos/p", {
    ...proyecto,
    version: 123,
  });
  await cambiarEstadoProyecto("p", "finalizado", 123);
  expect(cliente.post).toHaveBeenLastCalledWith("/api/tareas/proyectos/p/estado", {
    estado: "finalizado",
    version: 123,
  });
});
