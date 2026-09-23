// ============================================================
// Permisos de Proyecto — LÓGICA PURA. Sin React, sin Promise, sin I/O.
// Espejo reducido de `maquinaEstadosTarea.ts`: Proyecto no tiene una
// máquina de estados con transiciones condicionadas (Abierto → Finalizado
// o Cancelado son cambios directos, sin comentario/solución obligatorios
// como en Tarea), solo permisos por rol.
// ============================================================
import { puedeAsignarComoResponsable } from "./maquinaEstadosTarea";
import type { ActorTarea, PersonaCandidata, Rol } from "../types";

/** Error de dominio: un guard de Proyecto rechazó la acción. */
export class ErrorDominioProyecto extends Error {
  constructor(mensaje: string) {
    super(mensaje);
    this.name = "ErrorDominioProyecto";
  }
}

const ROLES_QUE_GESTIONAN_PROYECTOS: readonly Rol[] = ["Decanato", "Secretaría Académica"];

/** ¿El actor puede crear proyectos? Controla la visibilidad del botón "Nuevo Proyecto". */
export function puedeCrearProyecto(actor: ActorTarea): boolean {
  return ROLES_QUE_GESTIONAN_PROYECTOS.includes(actor.rol);
}

/** ¿El actor puede cambiar el estado de un Proyecto? Por rol, no por ownership. */
export function puedeCambiarEstadoProyecto(actor: ActorTarea): boolean {
  return ROLES_QUE_GESTIONAN_PROYECTOS.includes(actor.rol);
}

/**
 * ¿El actor puede asignarle este candidato como Responsable de Proyecto?
 * Combina la jerarquía de asignación de Tareas con el universo acotado de
 * Proyecto: el candidato MUST ser Secretaría o Decanato.
 */
export function puedeAsignarComoResponsableProyecto(
  actor: ActorTarea,
  candidato: PersonaCandidata,
): boolean {
  return (
    ROLES_QUE_GESTIONAN_PROYECTOS.includes(candidato.rol) &&
    puedeAsignarComoResponsable(actor, candidato)
  );
}
