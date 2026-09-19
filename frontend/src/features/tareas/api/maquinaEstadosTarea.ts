// ============================================================
// Máquina de estados de las tareas — LÓGICA PURA.
// Sin React, sin Promise, sin I/O: dada (tarea, acción, actor),
// valida los guards y devuelve una tarea NUEVA o lanza
// ErrorDominioTarea. Espejo de designaciones/api/maquinaEstados.ts.
// ============================================================
import type {
  AccionHistorialTarea,
  ActorTarea,
  DatosEditablesTarea,
  EstadoTarea,
  EventoHistorialTarea,
  Rol,
  Tarea,
} from "../types";

/** Error de dominio: un guard de la máquina de estados rechazó la acción. */
export class ErrorDominioTarea extends Error {
  constructor(mensaje: string) {
    super(mensaje);
    this.name = "ErrorDominioTarea";
  }
}

export type AccionTarea =
  | { tipo: "cambiarEstado"; estadoDestino: EstadoTarea; comentario?: string; solucion?: string }
  | { tipo: "editarAvance"; porcentajeAvance: number }
  | { tipo: "editar"; datos: DatosEditablesTarea };

const ROLES_QUE_CREAN: readonly Rol[] = ["Secretaría", "Decanato", "Administración"];

/** ¿El actor puede crear tareas? Controla la visibilidad del botón "Nueva Tarea". */
export function puedeCrearTarea(actor: ActorTarea): boolean {
  return ROLES_QUE_CREAN.includes(actor.rol);
}

function esLaAutoridadCreadora(tarea: Tarea, actor: ActorTarea): boolean {
  return actor.nombre === tarea.creadoPor.nombre;
}

function esElResponsable(tarea: Tarea, actor: ActorTarea): boolean {
  return actor.nombre === tarea.responsable.nombre;
}

/** Título/Descripción/fechas/Prioridad/Responsable: exclusivos de la autoridad creadora. */
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

function nuevoEvento(
  accion: AccionHistorialTarea,
  actor: ActorTarea,
  estado: EstadoTarea,
  detalle?: string,
): EventoHistorialTarea {
  return {
    id: crypto.randomUUID(),
    accion,
    porRol: actor.rol,
    porNombre: actor.nombre,
    estado,
    detalle,
    fecha: new Date().toISOString(),
  };
}

function conEvento(tarea: Tarea, evento: EventoHistorialTarea): Tarea {
  return { ...tarea, historial: [...tarea.historial, evento] };
}

/** Exige un texto no vacío para la transición dada. */
function requerirTexto(texto: string | undefined, mensaje: string): string {
  if (!texto || texto.trim() === "") {
    throw new ErrorDominioTarea(mensaje);
  }
  return texto;
}

function cambiarEstado(
  tarea: Tarea,
  actor: ActorTarea,
  estadoDestino: EstadoTarea,
  comentario: string | undefined,
  solucion: string | undefined,
): Tarea {
  if (!puedeCambiarEstado(tarea, actor, estadoDestino)) {
    if (estadoDestino === "cancelada") {
      throw new ErrorDominioTarea("Solo la autoridad creadora puede cancelar la tarea.");
    }
    throw new ErrorDominioTarea(
      `El estado "${tarea.estado}" es terminal: solo la autoridad creadora puede reabrirlo.`,
    );
  }

  let siguiente: Tarea = { ...tarea, estado: estadoDestino };

  if (estadoDestino === "pausa") {
    const motivo = requerirTexto(
      comentario,
      "Pasar a Pausa exige un comentario con el motivo de la consulta.",
    );
    siguiente = {
      ...siguiente,
      comentarios: [
        ...siguiente.comentarios,
        {
          id: crypto.randomUUID(),
          autor: actor.nombre,
          rolAutor: actor.rol,
          texto: motivo,
          fecha: new Date().toISOString(),
        },
      ],
    };
  }

  if (estadoDestino === "resuelta") {
    const detalle = requerirTexto(solucion, "Pasar a Resuelta exige completar el campo Solución.");
    siguiente = { ...siguiente, solucion: detalle };
  }

  return conEvento(siguiente, nuevoEvento("cambiar_estado", actor, estadoDestino));
}

function editarAvance(tarea: Tarea, actor: ActorTarea, porcentajeAvance: number): Tarea {
  if (!puedeEditarAvance(tarea, actor)) {
    throw new ErrorDominioTarea("Solo el Responsable o la autoridad creadora editan el avance.");
  }
  if (!Number.isFinite(porcentajeAvance) || porcentajeAvance < 0 || porcentajeAvance > 100) {
    throw new ErrorDominioTarea("El porcentaje de avance debe estar entre 0 y 100.");
  }
  const siguiente: Tarea = { ...tarea, porcentajeAvance };
  return conEvento(
    siguiente,
    nuevoEvento("editar_avance", actor, siguiente.estado, `${porcentajeAvance}%`),
  );
}

function editar(tarea: Tarea, actor: ActorTarea, datos: DatosEditablesTarea): Tarea {
  if (!puedeEditarCampos(tarea, actor)) {
    throw new ErrorDominioTarea("Solo la autoridad creadora puede editar los campos de la tarea.");
  }
  const siguiente: Tarea = { ...tarea, ...datos };
  return conEvento(siguiente, nuevoEvento("editar", actor, siguiente.estado));
}

/**
 * Valida los guards de la acción y devuelve la tarea resultante,
 * o lanza ErrorDominioTarea. No muta la tarea recibida.
 */
export function aplicarAccionTarea(tarea: Tarea, accion: AccionTarea, actor: ActorTarea): Tarea {
  switch (accion.tipo) {
    case "cambiarEstado":
      return cambiarEstado(tarea, actor, accion.estadoDestino, accion.comentario, accion.solucion);
    case "editarAvance":
      return editarAvance(tarea, actor, accion.porcentajeAvance);
    case "editar":
      return editar(tarea, actor, accion.datos);
    default: {
      const accionNoSoportada: never = accion;
      throw new ErrorDominioTarea(
        `Acción no soportada: ${(accionNoSoportada as AccionTarea).tipo}`,
      );
    }
  }
}
