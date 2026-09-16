import { apiClient } from "../../../shared/api/client";

export async function exportarLote(periodoId: string): Promise<Blob> {
  return (
    await apiClient.get<Blob>(`/api/designaciones/periodos/${periodoId}/lote.xlsx`, {
      responseType: "blob",
    })
  ).data;
}
