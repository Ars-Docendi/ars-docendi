import { apiClient } from "../../api/client";
import type { IdentidadDesarrollo } from "./types";

export async function listarIdentidadesDesarrollo(): Promise<IdentidadDesarrollo[]> {
  const respuesta = await apiClient.get<IdentidadDesarrollo[]>("/api/desarrollo/identidades");
  return respuesta.data;
}
