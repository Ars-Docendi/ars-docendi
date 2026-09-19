import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  agregarComentario,
  cambiarEstadoTarea,
  crearTarea,
  editarAvance,
  editarTarea,
} from "../api/tareasApi";
import type { ActorTarea, DatosEditablesTarea, EstadoTarea } from "../types";

interface ParamsEditar {
  id: string;
  datos: DatosEditablesTarea;
}

interface ParamsCambiarEstado {
  id: string;
  estadoDestino: EstadoTarea;
  comentario?: string;
  solucion?: string;
}

interface ParamsEditarAvance {
  id: string;
  porcentajeAvance: number;
}

interface ParamsComentario {
  id: string;
  texto: string;
}

/** Crea una tarea en Pendiente. Invalida el listado al terminar. */
export function useCrearTarea(actor: ActorTarea) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (datos: DatosEditablesTarea) => crearTarea(datos, actor),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tareas"] }),
  });
}

/** Edita los campos de una tarea (exclusivo de la autoridad creadora). */
export function useEditarTarea(actor: ActorTarea) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, datos }: ParamsEditar) => editarTarea(id, datos, actor),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tareas"] }),
  });
}

/** Cambia el estado de una tarea (Pausa exige comentario, Resuelta exige solución). */
export function useCambiarEstadoTarea(actor: ActorTarea) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, estadoDestino, comentario, solucion }: ParamsCambiarEstado) =>
      cambiarEstadoTarea(id, estadoDestino, actor, { comentario, solucion }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tareas"] }),
  });
}

/** Actualiza el % de avance (Responsable o autoridad creadora). */
export function useEditarAvance(actor: ActorTarea) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, porcentajeAvance }: ParamsEditarAvance) =>
      editarAvance(id, porcentajeAvance, actor),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tareas"] }),
  });
}

/** Agrega un comentario interno al hilo de la tarea. */
export function useAgregarComentario(actor: ActorTarea) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, texto }: ParamsComentario) => agregarComentario(id, actor, texto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tareas"] }),
  });
}
