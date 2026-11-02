// ============================================================
// Filtro preseleccionado del listado: Pendientes / Terminadas / Todas. No es un filtro
// aparte: al elegirlo se carga el filtro de la columna Estado con los estados que
// corresponden, y ese filtro sigue pudiendo editarse a mano. Sirve para abrir la pantalla
// sin sobrecargarla con tareas que ya no requieren atención.
// ============================================================
import type { EstadoTarea } from "../types";

export type PresetEstado = "pendientes" | "terminadas" | "todas";

export const PRESET_INICIAL: PresetEstado = "pendientes";

export const ETIQUETA_PRESET: Record<PresetEstado, string> = {
  pendientes: "Pendientes",
  terminadas: "Terminadas",
  todas: "Todas",
};

const ESTADOS_POR_PRESET: Record<PresetEstado, EstadoTarea[]> = {
  pendientes: ["pendiente", "en_curso", "pausa"],
  terminadas: ["resuelta", "cancelada"],
  todas: [],
};

/** Estados que selecciona el preset en el filtro de la columna Estado (vacío = sin acotar). */
export function estadosDePreset(preset: PresetEstado): EstadoTarea[] {
  return [...ESTADOS_POR_PRESET[preset]];
}
