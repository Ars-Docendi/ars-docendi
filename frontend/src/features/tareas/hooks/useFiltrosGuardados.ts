import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { guardar, leerDeUsuario, type ConfiguracionFiltro } from "../api/filtrosGuardadosStore";
import type { FiltrosTareasState } from "../components/filtrosTareas";
import type { ActorTarea } from "../types";

/** Configuraciones de filtros guardadas por el actor actual. */
export function useFiltrosGuardados(actor: ActorTarea) {
  return useQuery({
    queryKey: ["tareas", "filtros-guardados", actor.nombre],
    queryFn: () => leerDeUsuario(actor.nombre),
  });
}

/** Guarda la combinación actual de filtros con un nombre elegido por el usuario. */
export function useGuardarFiltros(actor: ActorTarea) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ nombre, filtros }: { nombre: string; filtros: FiltrosTareasState }) =>
      guardar({
        id: crypto.randomUUID(),
        nombre,
        propietario: actor.nombre,
        filtros,
      } satisfies ConfiguracionFiltro),
    onSuccess: () =>
      qc.invalidateQueries({ queryKey: ["tareas", "filtros-guardados", actor.nombre] }),
  });
}
