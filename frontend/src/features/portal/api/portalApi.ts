import { apiClient } from "../../../shared/api/client";
import { subirArchivo } from "../../../shared/api/archivosApi";
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

export async function guardarCv(archivo: File | { nombre: string }, version?: number | null) {
  if (!("arrayBuffer" in archivo)) {
    return (
      await apiClient.put("/api/portal/perfil/cv", { nombre: archivo.nombre, uri: null, version })
    ).data;
  }
  const confirmado = await subirArchivo("cv", archivo);
  return (
    await apiClient.put("/api/portal/perfil/cv", {
      archivoId: confirmado.id,
      version,
    })
  ).data;
}

export async function eliminarCv(version?: number | null) {
  await apiClient.delete("/api/portal/perfil/cv", { params: { version } });
}

export async function reemplazarTags(
  tipo: "habilidades" | "intereses",
  terminos: string[],
  version?: number | null,
) {
  await apiClient.put(`/api/portal/perfil/${tipo}`, { terminos, version });
}

export async function crearExperiencia(datos: DatosExperiencia) {
  return (await apiClient.post("/api/portal/perfil/experiencia", payloadPeriodo(datos))).data;
}
export async function editarExperiencia(id: string, datos: DatosExperiencia) {
  return (await apiClient.put(`/api/portal/perfil/experiencia/${id}`, payloadPeriodo(datos))).data;
}
export async function eliminarExperiencia(id: string, version?: number | null) {
  await apiClient.delete(`/api/portal/perfil/experiencia/${id}`, { params: { version } });
}

export async function crearEducacion(datos: DatosEducacion) {
  return (await apiClient.post("/api/portal/perfil/educacion", payloadPeriodo(datos))).data;
}
export async function editarEducacion(id: string, datos: DatosEducacion) {
  return (await apiClient.put(`/api/portal/perfil/educacion/${id}`, payloadPeriodo(datos))).data;
}
export async function eliminarEducacion(id: string, version?: number | null) {
  await apiClient.delete(`/api/portal/perfil/educacion/${id}`, { params: { version } });
}

export async function crearCertificacion(datos: DatosCertificacion) {
  return (await apiClient.post("/api/portal/perfil/certificaciones", datos)).data;
}
export async function editarCertificacion(id: string, datos: DatosCertificacion) {
  return (await apiClient.put(`/api/portal/perfil/certificaciones/${id}`, datos)).data;
}
export async function eliminarCertificacion(id: string, version?: number | null) {
  await apiClient.delete(`/api/portal/perfil/certificaciones/${id}`, { params: { version } });
}

export async function crearProyecto(datos: DatosProyecto) {
  return (await apiClient.post("/api/portal/perfil/proyectos", await payloadProyecto(datos))).data;
}
export async function editarProyecto(id: string, datos: DatosProyecto) {
  return (await apiClient.put(`/api/portal/perfil/proyectos/${id}`, await payloadProyecto(datos)))
    .data;
}
export async function eliminarProyecto(id: string, version?: number | null) {
  await apiClient.delete(`/api/portal/perfil/proyectos/${id}`, { params: { version } });
}

async function payloadProyecto({ documento, ...datos }: DatosProyecto) {
  let documentoArchivoId = documento?.archivoId || null;
  if (documento?.archivo) {
    const confirmado = await subirArchivo("documento_proyecto", documento.archivo);
    documentoArchivoId = confirmado.id;
  }
  return {
    ...payloadPeriodo(datos),
    ...(documentoArchivoId
      ? { documentoArchivoId }
      : { documentoNombre: documento?.nombre ?? null, documentoUri: null }),
  };
}

function payloadPeriodo<T extends { desde: string; hasta: string | null }>(datos: T) {
  const fecha = (valor: string) => valor.padEnd(10, "-01");
  return { ...datos, desde: fecha(datos.desde), hasta: datos.hasta ? fecha(datos.hasta) : null };
}
