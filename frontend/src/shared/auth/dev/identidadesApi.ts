import { apiClient } from "../../api/client";
import type { SesionApi } from "../sesionMicrosoft";
import type { IdentidadDesarrollo } from "./types";

export async function listarIdentidadesDesarrollo(): Promise<IdentidadDesarrollo[]> {
  const respuesta = await apiClient.get<IdentidadDesarrollo[]>("/api/desarrollo/identidades");
  return respuesta.data;
}

/** Misma forma que la sesión por cookie, resuelta con los headers de desarrollo. */
export async function obtenerSesionDesarrolloApi(): Promise<SesionApi> {
  return (await apiClient.get<SesionApi>("/api/desarrollo/sesion")).data;
}
