// ============================================================
// API de tareas — cliente HTTP de `Modules.Tareas` (`/api/tareas`). Las reglas
// (permisos, jerarquía de asignación, guards de estado) las valida el servidor: acá
// solo se traducen los Problem Details a un mensaje legible para la UI.
// ============================================================
import type { AxiosResponse } from "axios";
import { apiClient } from "../../../shared/api/client";
import { mensajeProblema } from "../../../shared/api/problemDetails";
import type { DatosEditablesTarea, EstadoTarea, PersonaCandidata, Tarea } from "../types";

/** Ejecuta la llamada y, si falla, lanza un `Error` con el mensaje del Problem Details. */
export async function pedir<T>(
  llamada: Promise<AxiosResponse<T>>,
  mensajePorDefecto = "No se pudo completar la operación.",
): Promise<T> {
  try {
    return (await llamada).data;
  } catch (error) {
    throw new Error(mensajeProblema(error, mensajePorDefecto), { cause: error });
  }
}

export function listarTareas(): Promise<Tarea[]> {
  return pedir(apiClient.get<Tarea[]>("/api/tareas"), "No se pudieron cargar las tareas.");
}

export function obtenerTarea(id: string): Promise<Tarea> {
  return pedir(apiClient.get<Tarea>(`/api/tareas/${id}`), "No se pudo cargar la tarea.");
}

/** Usuarios que el actor puede asignar como Responsable (el servidor aplica la jerarquía). */
export function listarCandidatos(paraProyecto = false): Promise<PersonaCandidata[]> {
  return pedir(
    apiClient.get<PersonaCandidata[]>("/api/tareas/candidatos", {
      params: paraProyecto ? { para: "proyecto" } : undefined,
    }),
    "No se pudieron cargar los candidatos a Responsable.",
  );
}

/** Crea una tarea; con `tareaPadreId` es una hija y hereda el Proyecto del padre. */
export function crearTarea(datos: DatosEditablesTarea, tareaPadreId?: string): Promise<Tarea> {
  return pedir(
    apiClient.post<Tarea>("/api/tareas", { ...datos, tareaPadreId }),
    "No se pudo crear la tarea.",
  );
}

export function editarTarea(id: string, datos: DatosEditablesTarea): Promise<Tarea> {
  return pedir(apiClient.put<Tarea>(`/api/tareas/${id}`, datos), "No se pudo editar la tarea.");
}

export function cambiarEstadoTarea(
  id: string,
  estado: EstadoTarea,
  opciones: { comentario?: string; solucion?: string } = {},
): Promise<Tarea> {
  return pedir(
    apiClient.post<Tarea>(`/api/tareas/${id}/estado`, { estado, ...opciones }),
    "No se pudo cambiar el estado.",
  );
}

export function editarAvance(id: string, porcentajeAvance: number): Promise<Tarea> {
  return pedir(
    apiClient.patch<Tarea>(`/api/tareas/${id}/avance`, { porcentajeAvance }),
    "No se pudo actualizar el avance.",
  );
}

export function agregarComentario(id: string, texto: string): Promise<Tarea> {
  return pedir(
    apiClient.post<Tarea>(`/api/tareas/${id}/comentarios`, { texto }),
    "No se pudo agregar el comentario.",
  );
}

export async function agregarRelacion(id: string, otraTareaId: string): Promise<void> {
  await pedir(
    apiClient.post<void>(`/api/tareas/${id}/relaciones`, { otraTareaId }),
    "No se pudo relacionar las tareas.",
  );
}

export async function quitarRelacion(id: string, otraId: string): Promise<void> {
  await pedir(
    apiClient.delete<void>(`/api/tareas/${id}/relaciones/${otraId}`),
    "No se pudo quitar la relación.",
  );
}
