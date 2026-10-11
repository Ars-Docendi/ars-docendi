// ============================================================
// Predicados de visibilidad de las acciones sobre una tarea — LÓGICA PURA.
// La fuente de verdad es el servidor (`Modules.Tareas`, MaquinaEstadosTarea): estos
// predicados solo anticipan sus guards para mostrar u ocultar controles. Los permisos
// de crear tareas/proyectos ya no viven acá: salen de `user.permissions`
// (`tareas.gestionar` / `proyectos.gestionar`, ver `hooks/usePermisosTareas.ts`).
// ============================================================
import type { ActorTarea, EstadoTarea, Tarea } from "../types";

// El Administrador de Sistemas puede todo: cuenta como autoridad creadora y como Responsable.
const ROL_ADMINISTRADOR = "Administrador de Sistemas";

function esLaAutoridadCreadora(tarea: Tarea, actor: ActorTarea): boolean {
  return actor.rol === ROL_ADMINISTRADOR || actor.nombre === tarea.creadoPor.nombre;
}

function esElResponsable(tarea: Tarea, actor: ActorTarea): boolean {
  return actor.rol === ROL_ADMINISTRADOR || actor.nombre === tarea.responsable.nombre;
}

/** Título/Descripción/fechas/Prioridad/Tipo/Responsable/Proyecto: exclusivos de la autoridad creadora. */
export function puedeEditarCampos(tarea: Tarea, actor: ActorTarea): boolean {
  return esLaAutoridadCreadora(tarea, actor);
}

/** % de avance y Solución: los completa el Responsable (o la autoridad creadora). */
export function puedeEditarAvance(tarea: Tarea, actor: ActorTarea): boolean {
  return esElResponsable(tarea, actor) || esLaAutoridadCreadora(tarea, actor);
}

const ESTADOS_TERMINALES: readonly EstadoTarea[] = ["resuelta", "cancelada"];

function esTerminal(estado: EstadoTarea): boolean {
  return ESTADOS_TERMINALES.includes(estado);
}

/**
 * ¿El actor puede llevar la tarea al estado destino indicado?
 * - Cancelar: exclusivo de la autoridad creadora, y solo desde un estado no terminal.
 * - Cualquier otro destino: el Responsable puede moverla libremente; si la tarea
 *   ya está en un estado terminal (resuelta/cancelada), solo la autoridad
 *   creadora puede reabrirla/revertirla.
 */
export function puedeCambiarEstado(
  tarea: Tarea,
  actor: ActorTarea,
  estadoDestino: EstadoTarea,
): boolean {
  if (estadoDestino === "cancelada") {
    return esLaAutoridadCreadora(tarea, actor) && !esTerminal(tarea.estado);
  }
  if (esTerminal(tarea.estado)) {
    return esLaAutoridadCreadora(tarea, actor);
  }
  return esElResponsable(tarea, actor) || esLaAutoridadCreadora(tarea, actor);
}
