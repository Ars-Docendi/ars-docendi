import { apiClient } from "../../../shared/api/client";
import type { ConversacionDetalle, ConversacionResumen } from "../types";

// ============================================================
// Lectura de soporte del historial AJENO
// (asistente-acceso-de-soporte-al-historial). Los dos endpoints son POST y no
// GET a propósito (design.md D10): la razón obligatoria viaja en el cuerpo y
// nunca en la URL, donde terminaría en un log de acceso o el historial del
// navegador.
// ============================================================

/** Lista las conversaciones de OTRO actor. Audita como listado, con razón. */
export async function listarHistorialDeSoporte(
  actorId: string,
  razon: string,
): Promise<ConversacionResumen[]> {
  const { data } = await apiClient.post<ConversacionResumen[]>(
    `/api/asistente/soporte/historial/${actorId}/listar`,
    { razon },
  );
  return data;
}

/** Lee una conversación de OTRO actor. Audita nombrándola, con razón. */
export async function leerHistorialDeSoporte(
  actorId: string,
  hiloId: string,
  razon: string,
): Promise<ConversacionDetalle> {
  const { data } = await apiClient.post<ConversacionDetalle>(
    `/api/asistente/soporte/historial/${actorId}/${hiloId}/leer`,
    { razon },
  );
  return data;
}

/** Una persona, tal como la necesita el selector de sujeto de la pantalla de soporte. */
export interface PersonaParaSoporte {
  id: string;
  nombre: string;
  apellido: string;
  documento: string;
}

/**
 * Busca personas para elegir A QUIÉN leerle el historial.
 *
 * REUTILIZA EL ENDPOINT EXISTENTE de administración de usuarios
 * (`GET /api/administracion/usuarios`) en vez de crear un buscador nuevo —es
 * lo que tasks.md 13.1 pide explícitamente—, pero NO importa nada de
 * `features/usuarios/`: esta feature no cruza esa frontera (react-features-guide,
 * «las features no se importan entre sí»), así que llama al mismo endpoint
 * real por su cuenta y con la forma mínima que necesita este selector.
 *
 * Exige `usuarios.ver` en el backend, igual que la pantalla de administración
 * de usuarios: quien administra los roles decide si otorga los dos permisos
 * juntos a quien vaya a hacer soporte del historial.
 */
export async function buscarPersonasParaSoporte(): Promise<PersonaParaSoporte[]> {
  const { data } = await apiClient.get<
    { id: string; nombre: string; apellido: string; documento: string }[]
  >("/api/administracion/usuarios");
  return data.map(({ id, nombre, apellido, documento }) => ({ id, nombre, apellido, documento }));
}
