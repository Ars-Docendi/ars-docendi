import axios from "axios";
import { apiClient } from "../../../shared/api/client";

export async function exportarLote(periodoId: string): Promise<Blob> {
  return (
    await apiClient.get<Blob>(`/api/designaciones/periodos/${periodoId}/lote.xlsx`, {
      responseType: "blob",
    })
  ).data;
}

/**
 * ¿El backend bloqueó el lote porque la cadena de integridad del período no verifica?
 * El problema llega como Blob por el `responseType` del archivo.
 */
export async function esIntegridadComprometida(error: unknown): Promise<boolean> {
  const datos: unknown = axios.isAxiosError(error) ? error.response?.data : undefined;
  if (!(datos instanceof Blob)) return false;
  try {
    const problema = JSON.parse(await datos.text()) as { type?: string };
    return problema.type?.endsWith("lote-integridad-comprometida") === true;
  } catch {
    return false;
  }
}
