import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  aceptarPedido,
  crearPedido,
  despriorizarPedido,
  devolverPedido,
  editarPedido,
  eliminarPedido,
  enviarPedido,
  priorizarPedido,
  rechazarPedido,
  reenviarPedido,
} from "../api/pedidosApi";
import type { ActorContexto, DatosEditablesPedido } from "../types";

interface ParamsEditar {
  id: string;
  datos: DatosEditablesPedido;
}

/** Params de una acción de revisión con comentario (rechazar/devolver/priorizar). */
interface ParamsConComentario {
  id: string;
  comentario: string;
}

/** Crea un pedido en borrador. Invalida las queries de pedidos al terminar. */
export function useCrearPedido(actor: ActorContexto) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (datos: DatosEditablesPedido) => crearPedido(datos, actor),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Edita un pedido (borrador o devuelto del propietario). */
export function useEditarPedido(actor: ActorContexto) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, datos }: ParamsEditar) => editarPedido(id, datos, actor),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Envía un borrador a revisión (→ en_revision_coordinador). */
export function useEnviarPedido(actor: ActorContexto) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => enviarPedido(id, actor),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Elimina un pedido en borrador (no es una transición de estado: lo saca del store). */
export function useEliminarPedido(actor: ActorContexto) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => eliminarPedido(id, actor),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

// ============================================================
// SCRUM-8 — Mutations del circuito de revisión.
// ============================================================

/** Acepta un pedido (avanza la cadena). El comentario es opcional. */
export function useAceptarPedido(actor: ActorContexto) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, comentario }: { id: string; comentario?: string }) =>
      aceptarPedido(id, actor, comentario),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Rechaza un pedido (→ rechazado, terminal). Justificativo obligatorio. */
export function useRechazarPedido(actor: ActorContexto) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, comentario }: ParamsConComentario) => rechazarPedido(id, actor, comentario),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Devuelve un pedido un nivel atrás (→ devuelto). Comentario obligatorio. */
export function useDevolverPedido(actor: ActorContexto) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, comentario }: ParamsConComentario) => devolverPedido(id, actor, comentario),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Reenvía un pedido devuelto (retoma su etapa de retorno). */
export function useReenviarPedido(actor: ActorContexto) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => reenviarPedido(id, actor),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Marca un pedido como prioritario (sin cambiar el estado). Justificativo obligatorio. */
export function usePriorizarPedido(actor: ActorContexto) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, comentario }: ParamsConComentario) => priorizarPedido(id, actor, comentario),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Quita la marca de prioritario de un pedido (sin cambiar el estado). Comentario opcional. */
export function useDespriorizarPedido(actor: ActorContexto) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, comentario }: { id: string; comentario?: string }) =>
      despriorizarPedido(id, actor, comentario),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}
