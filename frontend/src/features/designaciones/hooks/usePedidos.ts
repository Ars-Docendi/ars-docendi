import { useQuery } from "@tanstack/react-query";
import { listarMisPedidos, listarPedidosPorAmbito, obtenerPedido } from "../api/pedidosApi";

/** Lista los pedidos de la cátedra del Jefe de Cátedra (su ámbito). */
export function useMisPedidos() {
  return useQuery({
    queryKey: ["pedidos", "mis-pedidos"],
    queryFn: listarMisPedidos,
  });
}

/** Lista los pedidos del ámbito del revisor (Coordinador→su carrera; Secretaría/Decanato/Administración→depto). */
export function usePedidosPorAmbito() {
  return useQuery({
    queryKey: ["pedidos", "ambito"],
    queryFn: listarPedidosPorAmbito,
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
