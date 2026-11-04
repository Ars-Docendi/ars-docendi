import { apiClient } from "../../../shared/api/client";
import type { BusquedaDeMenciones, TipoDeMencion } from "../types";

// ============================================================
// `GET /api/asistente/menciones` (asistente-menciones, design.md D10 de
// asistente-rediseno-v3). Ver docs/architecture/api-contracts.md §Asistente.
// ============================================================

export interface OpcionesDeBusquedaDeMenciones {
  /** Para soltar el request desde afuera: una tecla más deja obsoleto el anterior. */
  signal?: AbortSignal;
}

/** Busca materias o docentes para el popover de menciones. */
export async function buscarMenciones(
  tipo: TipoDeMencion,
  q: string,
  { signal }: OpcionesDeBusquedaDeMenciones = {},
): Promise<BusquedaDeMenciones> {
  const { data } = await apiClient.get<BusquedaDeMenciones>("/api/asistente/menciones", {
    params: { tipo, q },
    signal,
  });
  return data;
}
