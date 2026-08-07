import { useQuery } from "@tanstack/react-query";
import { listarMisPedidos, listarPedidosPorAmbito, obtenerPedido } from "../api/pedidosApi";
import type { ActorContexto } from "../types";

/** Lista los pedidos de la cátedra del Jefe de Cátedra (su ámbito). */
export function useMisPedidos(actor: ActorContexto) {
  return useQuery({
    queryKey: [
      "pedidos",
      "mis-pedidos",
      actor.rol,
      actor.catedras?.join("·") ?? actor.carrera ?? null,
    ],
    queryFn: () => listarMisPedidos(actor),
  });
}

/** Lista los pedidos del ámbito del revisor (Coordinador→su carrera; Secretaría/Decanato/Administración→depto). */
export function usePedidosPorAmbito(actor: ActorContexto) {
  return useQuery({
    queryKey: ["pedidos", "ambito", actor.rol, actor.carrera ?? null],
    queryFn: () => listarPedidosPorAmbito(actor),
  });
}

/** Obtiene un pedido por id. Inactivo (sin fetch) si no hay id (modo alta). */
export function usePedido(id: string | undefined) {
  return useQuery({
    queryKey: ["pedidos", id],
    queryFn: () => obtenerPedido(id ?? ""),
    enabled: Boolean(id),
  });
}
