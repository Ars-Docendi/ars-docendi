import { apiClient } from "../../../shared/api/client";
import type {
  ConversacionDetalle,
  ConversacionResumen,
  LoteDeBorrado,
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

/** Archiva una conversación propia (design.md D3 de asistente-rediseno-v3). */
export async function archivarConversacion(hiloId: string): Promise<void> {
  await apiClient.post(`/api/asistente/historial/${hiloId}/archivar`);
}

/** Desarchiva una conversación propia. */
export async function desarchivarConversacion(hiloId: string): Promise<void> {
  await apiClient.post(`/api/asistente/historial/${hiloId}/desarchivar`);
}

/**
 * Marca una conversación propia pendiente de borrado: desaparece de
 * inmediato de la lista, pero se puede deshacer con {@link deshacerBorrado}
 * dentro de su ventana (design.md D4 de asistente-rediseno-v3).
 */
export async function eliminarConversacion(hiloId: string): Promise<LoteDeBorrado> {
  const { data } = await apiClient.delete<LoteDeBorrado>(`/api/asistente/historial/${hiloId}`);
  return data;
}

/** Marca TODAS las conversaciones propias (archivadas incluidas) pendientes de borrado. */
export async function eliminarTodasLasConversaciones(): Promise<LoteDeBorrado> {
  const { data } = await apiClient.delete<LoteDeBorrado>("/api/asistente/historial");
  return data;
}

/** Deshace un lote de borrado propio, dentro de su ventana. */
export async function deshacerBorrado(lote: string): Promise<void> {
  await apiClient.post(`/api/asistente/historial/borrados/${lote}/deshacer`);
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
