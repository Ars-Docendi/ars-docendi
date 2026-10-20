import { useMutation, useQueryClient } from "@tanstack/react-query";
import { cambiarEstadoProyecto, crearProyecto, editarProyecto } from "../api/proyectosApi";
import { ConflictoTareasError } from "../api/tareasApi";
import type { DatosEditablesProyecto, EstadoProyecto } from "../types";

interface ParamsCambiarEstado {
  version: number;
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
  version: number;
  id: string;
  datos: DatosEditablesProyecto;
}

/** Guarda los datos editados de un proyecto (no su estado). Invalida el listado y el detalle. */
export function useEditarProyecto() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, datos, version }: ParamsEditar) => editarProyecto(id, datos, version),
    retry: false,
    onError: (error) => {
      if (error instanceof ConflictoTareasError)
        void qc.invalidateQueries({ queryKey: ["proyectos"] });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["proyectos"] }),
  });
}

/** Cambia el estado de un proyecto (Finalizado/Cancelado), restringido por permiso. */
export function useCambiarEstadoProyecto() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, estadoDestino, version }: ParamsCambiarEstado) =>
      cambiarEstadoProyecto(id, estadoDestino, version),
    retry: false,
    onError: (error) => {
      if (error instanceof ConflictoTareasError)
        void qc.invalidateQueries({ queryKey: ["proyectos"] });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["proyectos"] }),
  });
}
