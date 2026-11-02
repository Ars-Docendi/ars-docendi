import { apiClient } from "../../../shared/api/client";

export interface TerminoVocabulario {
  id: string;
  termino: string;
  sugerido: boolean;
  /** Término al que se fusionó; `null` si no está fusionado. */
  canonico: string | null;
  /** Perfiles que lo usan como habilidad o interés. */
  docentes: number;
}

export async function listarTerminos(): Promise<TerminoVocabulario[]> {
  return (await apiClient.get<TerminoVocabulario[]>("/api/portal/vocabulario/terminos")).data;
}

export async function incorporarTermino(id: string): Promise<void> {
  await apiClient.post(`/api/portal/vocabulario/terminos/${id}/incorporar`);
}

export async function renombrarTermino(id: string, termino: string): Promise<void> {
  await apiClient.put(`/api/portal/vocabulario/terminos/${id}`, { termino });
}

export async function fusionarTermino(id: string, destinoId: string): Promise<void> {
  await apiClient.post(`/api/portal/vocabulario/terminos/${id}/fusionar`, { destinoId });
}
