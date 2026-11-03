import type { Tarea } from "../types";

/**
 * Tareas que se pueden vincular como hijas: sin padre, no canceladas y que no sean la propia
 * tarea ni uno de sus ancestros (crearía un ciclo). El backend valida lo mismo.
 */
export function candidatasHija(tarea: Tarea, todas: Tarea[]): Tarea[] {
  const porId = new Map(todas.map((t) => [t.id, t]));
  const excluidas = new Set<string>([tarea.id]);
  for (let padreId = tarea.tareaPadreId; padreId && !excluidas.has(padreId);) {
    excluidas.add(padreId);
    padreId = porId.get(padreId)?.tareaPadreId;
  }
  return todas.filter((t) => !t.tareaPadreId && t.estado !== "cancelada" && !excluidas.has(t.id));
}
