import { apiClient } from "../../../shared/api/client";
import type {
  ConversacionDetalle,
  ConversacionResumen,
  ReanudarRespuesta,
  ReejecucionResultado,
} from "../types";

// ============================================================
// El historial PROPIO del actor (asistente-historial-conversaciones).
// Ver docs/architecture/api-contracts.md §Asistente — Historial.
// ============================================================

/** Lista las conversaciones propias, opcionalmente filtradas por texto. */
export async function listarConversaciones(q?: string): Promise<ConversacionResumen[]> {
  const { data } = await apiClient.get<ConversacionResumen[]>("/api/asistente/historial", {
    params: q ? { q } : undefined,
  });
  return data;
}

/** Una conversación propia, con sus turnos. */
export async function obtenerConversacion(hiloId: string): Promise<ConversacionDetalle> {
  const { data } = await apiClient.get<ConversacionDetalle>(`/api/asistente/historial/${hiloId}`);
  return data;
}

/** Renombra una conversación propia. */
export async function renombrarConversacion(hiloId: string, titulo: string): Promise<void> {
  await apiClient.patch(`/api/asistente/historial/${hiloId}`, { titulo });
}

/** Borra, permanentemente, una conversación propia. */
export async function eliminarConversacion(hiloId: string): Promise<void> {
  await apiClient.delete(`/api/asistente/historial/${hiloId}`);
}

/** Borra, permanentemente, TODAS las conversaciones propias. */
export async function eliminarTodasLasConversaciones(): Promise<void> {
  await apiClient.delete("/api/asistente/historial");
}

/**
 * Reanuda una conversación propia: siembra un hilo efímero nuevo con sus
 * turnos persistidos (design.md D3), listo para un seguimiento inmediato.
 */
export async function reanudarConversacion(hiloId: string): Promise<ReanudarRespuesta> {
  const { data } = await apiClient.post<ReanudarRespuesta>(
    `/api/asistente/historial/${hiloId}/reanudar`,
  );
  return data;
}

/**
 * «Volver a consultar»: re-ejecuta la SQL guardada de un turno propio ya
 * respondido, bajo el alcance ACTUAL del actor (design.md D4). Nunca llama
 * al modelo ni escribe una fila nueva de historial.
 */
export async function reejecutarTurno(turnoId: string): Promise<ReejecucionResultado> {
  const { data } = await apiClient.post<ReejecucionResultado>(
    `/api/asistente/historial/turnos/${turnoId}/reejecutar`,
  );
  return data;
}
