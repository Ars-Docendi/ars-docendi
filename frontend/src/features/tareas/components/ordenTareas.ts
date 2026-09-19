// ============================================================
// Orden del listado de tareas — lógica pura. Por defecto ordena por
// Fecha Inicio ascendente; clickear una columna del header ordena por
// esa columna (alternando asc/desc en clicks sucesivos sobre la misma).
// ============================================================
import type { Tarea } from "../types";

export type ClaveOrdenTarea =
  | "numero"
  | "titulo"
  | "autor"
  | "responsable"
  | "fechaInicio"
  | "fechaFin"
  | "prioridad"
  | "avance"
  | "estado";

export type DireccionOrden = "asc" | "desc";

export interface OrdenTareasState {
  clave: ClaveOrdenTarea;
  direccion: DireccionOrden;
}

export const ORDEN_INICIAL: OrdenTareasState = { clave: "fechaInicio", direccion: "asc" };

const RANGO_PRIORIDAD: Record<Tarea["prioridad"], number> = { baja: 1, media: 2, alta: 3 };
const RANGO_ESTADO: Record<Tarea["estado"], number> = {
  pendiente: 1,
  en_curso: 2,
  pausa: 3,
  resuelta: 4,
  cancelada: 5,
};

function valorComparable(tarea: Tarea, clave: ClaveOrdenTarea): string | number {
  switch (clave) {
    case "numero":
      return tarea.numero;
    case "titulo":
      return tarea.titulo.toLowerCase();
    case "autor":
      return tarea.creadoPor.nombre.toLowerCase();
    case "responsable":
      return tarea.responsable.nombre.toLowerCase();
    case "fechaInicio":
      return tarea.fechaInicio;
    case "fechaFin":
      return tarea.fechaFin;
    case "prioridad":
      return RANGO_PRIORIDAD[tarea.prioridad];
    case "avance":
      return tarea.porcentajeAvance;
    case "estado":
      return RANGO_ESTADO[tarea.estado];
  }
}

/** Devuelve una copia ordenada — no muta el array recibido. */
export function ordenarTareas(tareas: Tarea[], orden: OrdenTareasState): Tarea[] {
  const factor = orden.direccion === "asc" ? 1 : -1;
  return [...tareas].sort((a, b) => {
    const va = valorComparable(a, orden.clave);
    const vb = valorComparable(b, orden.clave);
    if (va < vb) return -1 * factor;
    if (va > vb) return 1 * factor;
    return 0;
  });
}

/** Click en una columna: si ya era la activa, alterna asc/desc; si no, arranca en asc. */
export function siguienteOrden(actual: OrdenTareasState, clave: ClaveOrdenTarea): OrdenTareasState {
  if (actual.clave === clave) {
    return { clave, direccion: actual.direccion === "asc" ? "desc" : "asc" };
  }
  return { clave, direccion: "asc" };
}
