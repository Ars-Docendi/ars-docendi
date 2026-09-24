import { useQuery } from "@tanstack/react-query";
import { listarProyectos, obtenerProyecto } from "../api/proyectosApi";

/** Lista todos los proyectos, sin importar su Estado — el listado completo es el mismo para todos los roles. */
export function useListadoProyectos() {
  return useQuery({
    queryKey: ["proyectos"],
    queryFn: () => listarProyectos(),
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
