// ============================================================
// Filtros del tablero de revisión (SCRUM-8, opción D). Lógica pura.
// - vista: "completa" (todo el ámbito, default) | "mis-pendientes" (solo los
//   pedidos en turno del actor). El default es "completa" para abrir con el board
//   lleno (los terminales no son "tu turno", así que "mis-pendientes" los oculta).
//   El filtro de vista lo aplica `TablaRevision` (necesita el actor); acá viven
//   los filtros de tipo/prioridad.
// - tipo: filtra por novedad.
// - prioridad: filtra por el flag de prioritario.
// ============================================================
import type { Novedad, PedidoDesignacion } from "../types";

export type VistaTablero = "mis-pendientes" | "completa";
export type FiltroTipo = "todos" | Novedad;
export type FiltroPrioridad = "todos" | "prioritarios" | "normales";

export interface FiltrosTablero {
  vista: VistaTablero;
  tipo: FiltroTipo;
  prioridad: FiltroPrioridad;
}

export const FILTROS_INICIALES: FiltrosTablero = {
  vista: "completa",
  tipo: "todos",
  prioridad: "todos",
};

/** Acota los pedidos por novedad y prioridad (la `vista` afecta las columnas, no las cards). */
export function aplicarFiltros(
  pedidos: PedidoDesignacion[],
  filtros: FiltrosTablero,
): PedidoDesignacion[] {
  return pedidos.filter((pedido) => {
    if (filtros.tipo !== "todos" && pedido.novedad !== filtros.tipo) return false;
    if (filtros.prioridad === "prioritarios" && !pedido.prioritario) return false;
    if (filtros.prioridad === "normales" && pedido.prioritario) return false;
    return true;
  });
}
