// ============================================================
// Filtros de "Mis pedidos" — lógica pura. Dos campos de texto siempre
// visibles (Docente, N°) + Tipo/Estado como filtros opcionales
// ("+ Añadir filtro"), mismo patrón que `FiltrosUsuarios.tsx`.
// ============================================================
import type { EstadoPedido, Novedad, PedidoDesignacion } from "../types";

export type FiltroEstado =
  | "todos"
  | "borrador"
  | "revision"
  | "aprobado"
  | "rechazado"
  | "devuelto"
  | "cancelado";

export interface FiltrosMisPedidosState {
  docente: string;
  numero: string;
  legajo: string;
  tipo: Novedad | "todos";
  estado: FiltroEstado;
  /** Índice de string: permite reusar el componente genérico `FiltrosLista`. */
  [clave: string]: string;
}

export const FILTROS_INICIALES: FiltrosMisPedidosState = {
  docente: "",
  numero: "",
  legajo: "",
  tipo: "todos",
  estado: "todos",
};

/** Minúsculas y sin diacríticos, para comparar sin distinguir mayúsculas/acentos. */
function normalizarTexto(texto: string): string {
  return texto
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(new RegExp("[\\u0300-\\u036f]", "g"), "");
}

function coincideEstado(estado: EstadoPedido, filtro: FiltroEstado): boolean {
  switch (filtro) {
    case "todos":
      return true;
    case "borrador":
      return estado === "borrador";
    case "revision":
      return estado.startsWith("en_revision");
    case "aprobado":
      return estado === "en_lote";
    case "rechazado":
      return estado === "rechazado";
    case "devuelto":
      return estado === "devuelto";
    case "cancelado":
      return estado === "cancelado";
  }
}

/** Acota los pedidos por los filtros activos (contiene, sin acentos/mayúsculas para texto). */
export function aplicarFiltrosMisPedidos(
  pedidos: PedidoDesignacion[],
  filtros: FiltrosMisPedidosState,
): PedidoDesignacion[] {
  const docente = normalizarTexto(filtros.docente);
  const numero = normalizarTexto(filtros.numero);
  const legajo = normalizarTexto(filtros.legajo);
  return pedidos.filter((pedido) => {
    if (docente && !normalizarTexto(pedido.docente.nombre).includes(docente)) return false;
    if (numero && !normalizarTexto(pedido.numero ?? "").includes(numero)) return false;
    if (legajo && !normalizarTexto(pedido.docente.legajo ?? "").includes(legajo)) return false;
    if (filtros.tipo !== "todos" && pedido.novedad !== filtros.tipo) return false;
    if (!coincideEstado(pedido.estado, filtros.estado)) return false;
    return true;
  });
}
