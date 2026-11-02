import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  ConflictoTareasError,
  agregarComentario,
  agregarRelacion,
  cambiarEstadoTarea,
  crearTarea,
  editarAvance,
  editarTarea,
  quitarRelacion,
} from "../api/tareasApi";
import type { DatosEditablesTarea, EstadoTarea } from "../types";

interface ParamsCrear {
  datos: DatosEditablesTarea;
  /** Presente al crear una tarea hija: id de la tarea padre. */
  tareaPadreId?: string;
}

interface ParamsEditar {
  version: number;
  id: string;
  datos: DatosEditablesTarea;
}

interface ParamsRelacion {
  id: string;
  otraId: string;
}

interface ParamsCambiarEstado {
  version: number;
  id: string;
  estadoDestino: EstadoTarea;
  comentario?: string;
  solucion?: string;
}

interface ParamsEditarAvance {
  version: number;
  id: string;
  porcentajeAvance: number;
}

interface ParamsComentario {
  id: string;
  texto: string;
}

/** Crea una tarea en Pendiente (o una hija, si se pasa `tareaPadreId`). Invalida el listado al terminar. */
export function useCrearTarea() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ datos, tareaPadreId }: ParamsCrear) => crearTarea(datos, tareaPadreId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tareas"] }),
  });
}

/** Edita los campos de una tarea (exclusivo de la autoridad creadora). */
export function useEditarTarea() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, datos, version }: ParamsEditar) => editarTarea(id, datos, version),
    retry: false,
    onError: (error) => {
      if (error instanceof ConflictoTareasError)
        void qc.invalidateQueries({ queryKey: ["tareas"] });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tareas"] }),
  });
}

/** Cambia el estado de una tarea (Pausa exige comentario, Resuelta exige solución). */
export function useCambiarEstadoTarea() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, estadoDestino, comentario, solucion, version }: ParamsCambiarEstado) =>
      cambiarEstadoTarea(id, estadoDestino, { comentario, solucion, version }),
    retry: false,
    onError: (error) => {
      if (error instanceof ConflictoTareasError)
        void qc.invalidateQueries({ queryKey: ["tareas"] });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tareas"] }),
  });
}

/** Actualiza el % de avance (Responsable o autoridad creadora). */
export function useEditarAvance() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, porcentajeAvance, version }: ParamsEditarAvance) =>
      editarAvance(id, porcentajeAvance, version),
    retry: false,
    onError: (error) => {
      if (error instanceof ConflictoTareasError)
        void qc.invalidateQueries({ queryKey: ["tareas"] });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tareas"] }),
  });
}

/** Agrega un comentario interno al hilo de la tarea. */
export function useAgregarComentario() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, texto }: ParamsComentario) => agregarComentario(id, texto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tareas"] }),
  });
}

/** Relaciona dos tareas entre sí (vínculo simple, bidireccional). */
export function useAgregarRelacion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, otraId }: ParamsRelacion) => agregarRelacion(id, otraId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tareas"] }),
  });
}

/** Quita la relación entre dos tareas (ambos lados). */
export function useQuitarRelacion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, otraId }: ParamsRelacion) => quitarRelacion(id, otraId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tareas"] }),
  });
}
