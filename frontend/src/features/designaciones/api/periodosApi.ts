import { apiClient } from "../../../shared/api/client";
import type { PeriodoDesignacion } from "../types";

export async function listarPeriodos(): Promise<PeriodoDesignacion[]> {
  return (await apiClient.get<PeriodoDesignacion[]>("/api/designaciones/periodos")).data;
}
export async function crearPeriodo(
  datos: Omit<PeriodoDesignacion, "id">,
): Promise<PeriodoDesignacion> {
  return (await apiClient.post<PeriodoDesignacion>("/api/designaciones/periodos", datos)).data;
}
export async function editarPeriodo(
  id: string,
  datos: Omit<PeriodoDesignacion, "id">,
): Promise<PeriodoDesignacion> {
  return (await apiClient.put<PeriodoDesignacion>(`/api/designaciones/periodos/${id}`, datos)).data;
}
export async function eliminarPeriodo(id: string): Promise<void> {
  await apiClient.delete(`/api/designaciones/periodos/${id}`);
}
