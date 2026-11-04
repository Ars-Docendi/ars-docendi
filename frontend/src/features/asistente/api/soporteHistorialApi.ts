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

/** Busca personas para elegir A QUIÉN leerle el historial. */
export async function buscarPersonasParaSoporte(): Promise<PersonaParaSoporte[]> {
  const { data } = await apiClient.get<
    {
      usuarioId: string | null;
      nombre: string;
      apellido: string;
      documento: string;
    }[]
  >("/api/administracion/personas");
  // Sólo quien tiene cuenta tiene historial que leer; `id` es el de la cuenta, no el de la persona.
  return data.flatMap(({ usuarioId, nombre, apellido, documento }) =>
    usuarioId ? [{ id: usuarioId, nombre, apellido, documento }] : [],
  );
}
