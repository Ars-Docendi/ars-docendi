import { useMutation, useQueryClient } from "@tanstack/react-query";
import { cambiarEstadoProyecto, crearProyecto } from "../api/proyectosApi";
import type { ActorTarea, DatosEditablesProyecto, EstadoProyecto } from "../types";

interface ParamsCambiarEstado {
  id: string;
  estadoDestino: EstadoProyecto;
}

/** Crea un proyecto en Abierto. Invalida el listado al terminar. */
export function useCrearProyecto(actor: ActorTarea) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (datos: DatosEditablesProyecto) => crearProyecto(datos, actor),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["proyectos"] }),
  });
}

/** Cambia el estado de un proyecto (Finalizado/Cancelado), restringido por rol. */
export function useCambiarEstadoProyecto(actor: ActorTarea) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, estadoDestino }: ParamsCambiarEstado) =>
      cambiarEstadoProyecto(id, estadoDestino, actor),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["proyectos"] }),
  });
}
