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
import type { DatosEditablesPedido } from "../types";
import { useCatalogosDesignaciones } from "./useCatalogosDesignaciones";

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
export function useCrearPedido() {
  const qc = useQueryClient();
  const catalogos = useCatalogosDesignaciones();
  return useMutation({
    mutationFn: (datos: DatosEditablesPedido) => crearPedido(datos, catalogos.data!),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Edita un pedido (borrador o devuelto del propietario). */
export function useEditarPedido() {
  const qc = useQueryClient();
  const catalogos = useCatalogosDesignaciones();
  return useMutation({
    mutationFn: ({ id, datos }: ParamsEditar) => editarPedido(id, datos, catalogos.data!),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Envía un borrador a revisión (→ en_revision_coordinador). */
export function useEnviarPedido() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: enviarPedido,
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Elimina un pedido en borrador (no es una transición de estado: lo saca del store). */
export function useEliminarPedido() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: eliminarPedido,
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

// ============================================================
// SCRUM-8 — Mutations del circuito de revisión.
// ============================================================

/** Acepta un pedido (avanza la cadena). El comentario es opcional. */
export function useAceptarPedido() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, comentario }: { id: string; comentario?: string }) =>
      aceptarPedido(id, comentario),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Rechaza un pedido (→ rechazado, terminal). Justificativo obligatorio. */
export function useRechazarPedido() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, comentario }: ParamsConComentario) => rechazarPedido(id, comentario),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Devuelve un pedido un nivel atrás (→ devuelto). Comentario obligatorio. */
export function useDevolverPedido() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, comentario }: ParamsConComentario) => devolverPedido(id, comentario),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Reenvía un pedido devuelto (retoma su etapa de retorno). */
export function useReenviarPedido() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: reenviarPedido,
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Marca un pedido como prioritario (sin cambiar el estado). Justificativo obligatorio. */
export function usePriorizarPedido() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, comentario }: ParamsConComentario) => priorizarPedido(id, comentario),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}

/** Quita la marca de prioritario de un pedido (sin cambiar el estado). Comentario opcional. */
export function useDespriorizarPedido() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, comentario }: { id: string; comentario?: string }) =>
      despriorizarPedido(id, comentario),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}
