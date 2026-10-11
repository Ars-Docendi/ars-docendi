// ============================================================
// Alcance del listado: todas las tareas o solo las propias. Convive con el filtro
// preseleccionado por estado (ver `presetEstado.ts`), así se combinan, por ejemplo,
// "Pendientes" + "Propias". Propias = las que creaste + las que tenés asignadas.
// ============================================================
import type { Tarea } from "../types";

export type AlcanceTareas = "todas" | "propias";

export const ALCANCE_INICIAL: AlcanceTareas = "todas";

export const ETIQUETA_ALCANCE: Record<AlcanceTareas, string> = {
  todas: "Todas",
  propias: "Propias",
};

/** ¿La tarea es propia del usuario: la creó o la tiene asignada? */
export function esPropia(tarea: Tarea, usuarioId: string): boolean {
  return tarea.creadoPor.id === usuarioId || tarea.responsable.id === usuarioId;
}

/** Acota las tareas según el alcance elegido. */
export function aplicarAlcance(
  tareas: Tarea[],
  alcance: AlcanceTareas,
  usuarioId: string,
): Tarea[] {
  return alcance === "propias" ? tareas.filter((t) => esPropia(t, usuarioId)) : tareas;
}
