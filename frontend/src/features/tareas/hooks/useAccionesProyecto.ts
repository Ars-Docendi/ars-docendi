import { useMutation, useQueryClient } from "@tanstack/react-query";
import { cambiarEstadoProyecto, crearProyecto, editarProyecto } from "../api/proyectosApi";
import type { DatosEditablesProyecto, EstadoProyecto } from "../types";

interface ParamsCambiarEstado {
  id: string;
  estadoDestino: EstadoProyecto;
}

/** Crea un proyecto en Abierto. Invalida el listado al terminar. */
export function useCrearProyecto() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (datos: DatosEditablesProyecto) => crearProyecto(datos),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["proyectos"] }),
  });
}

interface ParamsEditar {
  id: string;
  datos: DatosEditablesProyecto;
}

/** Guarda los datos editados de un proyecto (no su estado). Invalida el listado y el detalle. */
export function useEditarProyecto() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, datos }: ParamsEditar) => editarProyecto(id, datos),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["proyectos"] }),
  });
}

/** Cambia el estado de un proyecto (Finalizado/Cancelado), restringido por permiso. */
export function useCambiarEstadoProyecto() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, estadoDestino }: ParamsCambiarEstado) =>
      cambiarEstadoProyecto(id, estadoDestino),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["proyectos"] }),
  });
}
