import { useMutation, useQueryClient } from "@tanstack/react-query";
import { cancelarPedido, crearPedido, editarPedido, enviarPedido } from "../api/pedidosApi";
import type { ActorContexto, DatosEditablesPedido } from "../types";

interface ParamsEditar {
  id: string;
  datos: DatosEditablesPedido;
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

/** Cancela un borrador (→ cancelado). */
export function useCancelarPedido(actor: ActorContexto) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => cancelarPedido(id, actor),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}
