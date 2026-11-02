import { apiClient } from "../../../shared/api/client";

/** Designación vigente de quien consulta. Las horas de investigación y externas pueden faltar. */
export interface DesignacionPropia {
  id: string;
  materia: string;
  carrera: string;
  cargo: string;
  dedicacion: string | null;
  horas: number;
  horasInvestigacion: number | null;
  horasExternas: number | null;
  vigenteDesde: string;
}

export async function listarDesignacionesVigentes(): Promise<DesignacionPropia[]> {
  return (await apiClient.get<DesignacionPropia[]>("/api/designaciones/vigentes/mias")).data;
}
