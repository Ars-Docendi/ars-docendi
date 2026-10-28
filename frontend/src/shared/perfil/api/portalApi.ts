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

/**
 * Operaciones sobre un perfil de Mi Portal. La misma API sirve al perfil propio
 * (`/api/portal/perfil`) y al de otra persona desde la administración
 * (`/api/administracion/personas/{id}/portal`): sólo cambia la base.
 */
export interface ApiPerfil {
  obtenerPerfil(): Promise<PerfilDocente>;
  guardarContacto(datos: DatosContacto): Promise<unknown>;
  guardarCv(archivo: File | { nombre: string }, version?: number | null): Promise<unknown>;
  eliminarCv(version?: number | null): Promise<void>;
  descargarCv(): Promise<Blob>;
  descargarDocumento(proyectoId: string): Promise<Blob>;
  reemplazarTags(
    tipo: "habilidades" | "intereses",
    terminos: string[],
    version?: number | null,
  ): Promise<void>;
  crearExperiencia(datos: DatosExperiencia): Promise<unknown>;
  editarExperiencia(id: string, datos: DatosExperiencia): Promise<unknown>;
  eliminarExperiencia(id: string, version?: number | null): Promise<void>;
  crearEducacion(datos: DatosEducacion): Promise<unknown>;
  editarEducacion(id: string, datos: DatosEducacion): Promise<unknown>;
  eliminarEducacion(id: string, version?: number | null): Promise<void>;
  crearCertificacion(datos: DatosCertificacion): Promise<unknown>;
  editarCertificacion(id: string, datos: DatosCertificacion): Promise<unknown>;
  eliminarCertificacion(id: string, version?: number | null): Promise<void>;
  crearProyecto(datos: DatosProyecto): Promise<unknown>;
  editarProyecto(id: string, datos: DatosProyecto): Promise<unknown>;
  eliminarProyecto(id: string, version?: number | null): Promise<void>;
}

export function crearApiPerfil(base: string): ApiPerfil {
  const blob = async (url: string) =>
    (await apiClient.get<Blob>(url, { responseType: "blob" })).data;
  return {
    async obtenerPerfil() {
      return (await apiClient.get<PerfilDocente>(base)).data;
    },
    async guardarContacto(datos) {
      return (await apiClient.put(`${base}/contacto`, datos)).data;
    },
    async guardarCv(archivo, version) {
      if (!("arrayBuffer" in archivo)) {
        return (await apiClient.put(`${base}/cv`, { nombre: archivo.nombre, uri: null, version }))
          .data;
      }
      const confirmado = await subirArchivo("cv", archivo);
      return (await apiClient.put(`${base}/cv`, { archivoId: confirmado.id, version })).data;
    },
    async eliminarCv(version) {
      await apiClient.delete(`${base}/cv`, { params: { version } });
    },
    descargarCv: () => blob(`${base}/cv/descarga`),
    descargarDocumento: (proyectoId) => blob(`${base}/proyectos/${proyectoId}/documento`),
    async reemplazarTags(tipo, terminos, version) {
      await apiClient.put(`${base}/${tipo}`, { terminos, version });
    },
    async crearExperiencia(datos) {
      return (await apiClient.post(`${base}/experiencia`, payloadPeriodo(datos))).data;
    },
    async editarExperiencia(id, datos) {
      return (await apiClient.put(`${base}/experiencia/${id}`, payloadPeriodo(datos))).data;
    },
    async eliminarExperiencia(id, version) {
      await apiClient.delete(`${base}/experiencia/${id}`, { params: { version } });
    },
    async crearEducacion(datos) {
      return (await apiClient.post(`${base}/educacion`, payloadPeriodo(datos))).data;
    },
    async editarEducacion(id, datos) {
      return (await apiClient.put(`${base}/educacion/${id}`, payloadPeriodo(datos))).data;
    },
    async eliminarEducacion(id, version) {
      await apiClient.delete(`${base}/educacion/${id}`, { params: { version } });
    },
    async crearCertificacion(datos) {
      return (await apiClient.post(`${base}/certificaciones`, datos)).data;
    },
    async editarCertificacion(id, datos) {
      return (await apiClient.put(`${base}/certificaciones/${id}`, datos)).data;
    },
    async eliminarCertificacion(id, version) {
      await apiClient.delete(`${base}/certificaciones/${id}`, { params: { version } });
    },
    async crearProyecto(datos) {
      return (await apiClient.post(`${base}/proyectos`, await payloadProyecto(datos))).data;
    },
    async editarProyecto(id, datos) {
      return (await apiClient.put(`${base}/proyectos/${id}`, await payloadProyecto(datos))).data;
    },
    async eliminarProyecto(id, version) {
      await apiClient.delete(`${base}/proyectos/${id}`, { params: { version } });
    },
  };
}

// Perfil propio (Mi Portal).
const propia = crearApiPerfil("/api/portal/perfil");
export const obtenerPerfil = () => propia.obtenerPerfil();
export const guardarContacto = (datos: DatosContacto) => propia.guardarContacto(datos);
export const guardarCv = (archivo: File | { nombre: string }, version?: number | null) =>
  propia.guardarCv(archivo, version);
export const eliminarCv = (version?: number | null) => propia.eliminarCv(version);
export const descargarCv = () => propia.descargarCv();
export const descargarDocumento = (proyectoId: string) => propia.descargarDocumento(proyectoId);
export const reemplazarTags = (
  tipo: "habilidades" | "intereses",
  terminos: string[],
  version?: number | null,
) => propia.reemplazarTags(tipo, terminos, version);
export const crearExperiencia = (datos: DatosExperiencia) => propia.crearExperiencia(datos);
export const editarExperiencia = (id: string, datos: DatosExperiencia) =>
  propia.editarExperiencia(id, datos);
export const eliminarExperiencia = (id: string, version?: number | null) =>
  propia.eliminarExperiencia(id, version);
export const crearEducacion = (datos: DatosEducacion) => propia.crearEducacion(datos);
export const editarEducacion = (id: string, datos: DatosEducacion) =>
  propia.editarEducacion(id, datos);
export const eliminarEducacion = (id: string, version?: number | null) =>
  propia.eliminarEducacion(id, version);
export const crearCertificacion = (datos: DatosCertificacion) => propia.crearCertificacion(datos);
export const editarCertificacion = (id: string, datos: DatosCertificacion) =>
  propia.editarCertificacion(id, datos);
export const eliminarCertificacion = (id: string, version?: number | null) =>
  propia.eliminarCertificacion(id, version);
export const crearProyecto = (datos: DatosProyecto) => propia.crearProyecto(datos);
export const editarProyecto = (id: string, datos: DatosProyecto) =>
  propia.editarProyecto(id, datos);
export const eliminarProyecto = (id: string, version?: number | null) =>
  propia.eliminarProyecto(id, version);

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
