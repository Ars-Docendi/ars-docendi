// ============================================================
// API de proyectos de Tareas — cliente HTTP de `/api/tareas/proyectos`. Crear y
// cambiar el estado requiere el permiso `proyectos.gestionar`; el servidor aplica el
// rol del Responsable y la jerarquía.
// ============================================================
import { apiClient } from "../../../shared/api/client";
import type {
  DatosEditablesProyecto,
  EstadoProyecto,
  EstadoProyectoCatalogo,
  Proyecto,
} from "../types";
import { pedir } from "./tareasApi";

export function listarProyectos(): Promise<Proyecto[]> {
  return pedir(
    apiClient.get<Proyecto[]>("/api/tareas/proyectos"),
    "No se pudieron cargar los proyectos.",
  );
}

/** Catálogo de estados de proyecto (de la base): qué estados existen y cuál admite tareas. */
export function listarEstadosProyecto(): Promise<EstadoProyectoCatalogo[]> {
  return pedir(
    apiClient.get<EstadoProyectoCatalogo[]>("/api/tareas/proyectos/estados"),
    "No se pudieron cargar los estados de proyecto.",
  );
}

export function obtenerProyecto(id: string): Promise<Proyecto> {
  return pedir(
    apiClient.get<Proyecto>(`/api/tareas/proyectos/${id}`),
    "No se pudo cargar el proyecto.",
  );
}

export function crearProyecto(datos: DatosEditablesProyecto): Promise<Proyecto> {
  return pedir(
    apiClient.post<Proyecto>("/api/tareas/proyectos", datos),
    "No se pudo crear el proyecto.",
  );
}

export function cambiarEstadoProyecto(id: string, estado: EstadoProyecto): Promise<Proyecto> {
  return pedir(
    apiClient.post<Proyecto>(`/api/tareas/proyectos/${id}/estado`, { estado }),
    "No se pudo cambiar el estado del proyecto.",
  );
}
