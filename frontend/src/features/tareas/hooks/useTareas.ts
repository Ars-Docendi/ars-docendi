import { useQuery } from "@tanstack/react-query";
import { listarCandidatos, listarTareas, obtenerTarea } from "../api/tareasApi";

/** Lista todas las tareas — el listado es el mismo para todos los roles. */
export function useListadoTareas() {
  return useQuery({
    queryKey: ["tareas"],
    queryFn: () => listarTareas(),
  });
}

/** Obtiene una tarea por id. Inactivo (sin fetch) si no hay id. */
export function useTarea(id: string | undefined) {
  return useQuery({
    queryKey: ["tareas", id],
    queryFn: () => obtenerTarea(id ?? ""),
    enabled: Boolean(id),
  });
}

/**
 * Usuarios que el actor puede asignar como Responsable de una tarea (o de un proyecto). El
 * servidor ya aplica la jerarquía; solo se consulta con el formulario abierto.
 */
export function useCandidatosResponsable(paraProyecto: boolean, habilitado: boolean) {
  return useQuery({
    queryKey: ["tareas-candidatos", paraProyecto],
    queryFn: () => listarCandidatos(paraProyecto),
    enabled: habilitado,
  });
}
