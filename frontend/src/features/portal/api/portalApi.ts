import { apiClient } from "../../../shared/api/client";
import type {
  DatosCertificacion,
  DatosContacto,
  DatosEducacion,
  DatosExperiencia,
  DatosProyecto,
  PerfilDocente,
} from "../types";

export const perfilKey = ["portal", "perfil"] as const;

export async function obtenerPerfil(): Promise<PerfilDocente> {
  const { data } = await apiClient.get<PerfilDocente>("/api/portal/perfil");
  return data;
}

export async function guardarContacto(datos: DatosContacto) {
  return (await apiClient.put("/api/portal/perfil/contacto", datos)).data;
}

export async function guardarCv({ nombre }: { nombre: string }) {
  return (await apiClient.put("/api/portal/perfil/cv", { nombre, uri: null })).data;
}

export async function eliminarCv() {
  await apiClient.delete("/api/portal/perfil/cv");
}

export async function reemplazarTags(tipo: "habilidades" | "intereses", terminos: string[]) {
  await apiClient.put(`/api/portal/perfil/${tipo}`, { terminos });
}

export async function crearExperiencia(datos: DatosExperiencia) {
  return (await apiClient.post("/api/portal/perfil/experiencia", payloadPeriodo(datos))).data;
}
export async function editarExperiencia(id: string, datos: DatosExperiencia) {
  return (await apiClient.put(`/api/portal/perfil/experiencia/${id}`, payloadPeriodo(datos))).data;
}
export async function eliminarExperiencia(id: string) {
  await apiClient.delete(`/api/portal/perfil/experiencia/${id}`);
}

export async function crearEducacion(datos: DatosEducacion) {
  return (await apiClient.post("/api/portal/perfil/educacion", payloadPeriodo(datos))).data;
}
export async function editarEducacion(id: string, datos: DatosEducacion) {
  return (await apiClient.put(`/api/portal/perfil/educacion/${id}`, payloadPeriodo(datos))).data;
}
export async function eliminarEducacion(id: string) {
  await apiClient.delete(`/api/portal/perfil/educacion/${id}`);
}

export async function crearCertificacion(datos: DatosCertificacion) {
  return (await apiClient.post("/api/portal/perfil/certificaciones", datos)).data;
}
export async function editarCertificacion(id: string, datos: DatosCertificacion) {
  return (await apiClient.put(`/api/portal/perfil/certificaciones/${id}`, datos)).data;
}
export async function eliminarCertificacion(id: string) {
  await apiClient.delete(`/api/portal/perfil/certificaciones/${id}`);
}

export async function crearProyecto(datos: DatosProyecto) {
  return (await apiClient.post("/api/portal/perfil/proyectos", payloadProyecto(datos))).data;
}
export async function editarProyecto(id: string, datos: DatosProyecto) {
  return (await apiClient.put(`/api/portal/perfil/proyectos/${id}`, payloadProyecto(datos))).data;
}
export async function eliminarProyecto(id: string) {
  await apiClient.delete(`/api/portal/perfil/proyectos/${id}`);
}

function payloadProyecto({ documento, ...datos }: DatosProyecto) {
  return {
    ...payloadPeriodo(datos),
    documentoNombre: documento?.nombre ?? null,
    documentoUri: null,
  };
}

function payloadPeriodo<T extends { desde: string; hasta: string | null }>(datos: T) {
  const fecha = (valor: string) => valor.padEnd(10, "-01");
  return { ...datos, desde: fecha(datos.desde), hasta: datos.hasta ? fecha(datos.hasta) : null };
}
