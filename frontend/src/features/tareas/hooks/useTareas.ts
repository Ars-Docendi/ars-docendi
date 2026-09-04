import { useQuery } from "@tanstack/react-query";
import { listarTareas, obtenerTarea } from "../api/tareasApi";

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
