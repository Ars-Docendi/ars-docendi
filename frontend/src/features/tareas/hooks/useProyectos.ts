import { useQuery } from "@tanstack/react-query";
import { listarEstadosProyecto, listarProyectos, obtenerProyecto } from "../api/proyectosApi";

/** Lista todos los proyectos, sin importar su Estado — el listado completo es el mismo para todos los roles. */
export function useListadoProyectos() {
  return useQuery({
    queryKey: ["proyectos"],
    queryFn: () => listarProyectos(),
  });
}

/** Catálogo de estados de proyecto; cambia muy rara vez, por eso se cachea sin refrescar. */
export function useEstadosProyecto() {
  return useQuery({
    queryKey: ["proyectos-estados"],
    queryFn: () => listarEstadosProyecto(),
    staleTime: Infinity,
  });
}

/** Obtiene un proyecto por id. Inactivo (sin fetch) si no hay id. */
export function useProyecto(id: string | undefined) {
  return useQuery({
    queryKey: ["proyectos", id],
    queryFn: () => obtenerProyecto(id ?? ""),
    enabled: Boolean(id),
  });
}
