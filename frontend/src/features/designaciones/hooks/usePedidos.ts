import { useQuery } from "@tanstack/react-query";
import { listarMisPedidos, obtenerPedido } from "../api/pedidosApi";
import type { ActorContexto } from "../types";

/** Lista los pedidos de la cátedra del Jefe de Cátedra (su ámbito). */
export function useMisPedidos(actor: ActorContexto) {
  return useQuery({
    queryKey: ["pedidos", "mis-pedidos", actor.rol, actor.catedra ?? actor.carrera ?? null],
    queryFn: () => listarMisPedidos(actor),
  });
}

/** Obtiene un pedido por id. */
export function usePedido(id: string) {
  return useQuery({
    queryKey: ["pedidos", id],
    queryFn: () => obtenerPedido(id),
  });
}
