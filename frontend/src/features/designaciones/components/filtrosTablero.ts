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
  nombre: string;
  legajo: string;
  /** Índice de string: permite reusar el componente genérico `FiltrosLista`. */
  [clave: string]: string;
}

export const FILTROS_INICIALES: FiltrosTablero = {
  vista: "completa",
  tipo: "todos",
  prioridad: "todos",
  nombre: "",
  legajo: "",
};

/** Minúsculas y sin diacríticos, para comparar sin distinguir mayúsculas/acentos. */
function normalizarTexto(texto: string): string {
  return texto
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(new RegExp("[\\u0300-\\u036f]", "g"), "");
}

/** Acota los pedidos por nombre/legajo del docente, novedad y prioridad (la `vista` afecta las columnas, no las cards). */
export function aplicarFiltros(
  pedidos: PedidoDesignacion[],
  filtros: FiltrosTablero,
): PedidoDesignacion[] {
  const nombre = normalizarTexto(filtros.nombre);
  const legajo = normalizarTexto(filtros.legajo);
  return pedidos.filter((pedido) => {
    if (filtros.tipo !== "todos" && pedido.novedad !== filtros.tipo) return false;
    if (filtros.prioridad === "prioritarios" && !pedido.prioritario) return false;
    if (filtros.prioridad === "normales" && pedido.prioritario) return false;
    if (nombre && !normalizarTexto(pedido.docente.nombre).includes(nombre)) return false;
    if (legajo && !normalizarTexto(pedido.docente.legajo ?? "").includes(legajo)) return false;
    return true;
  });
}
