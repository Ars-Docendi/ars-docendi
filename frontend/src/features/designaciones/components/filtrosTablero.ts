// ============================================================
// Filtros del tablero de revisión (SCRUM-8, opción D). Lógica pura.
// - vista: "completa" (todo el ámbito, default) | "mis-pendientes" (solo los
//   pedidos en turno del actor). El default es "completa" para abrir con el board
//   lleno (los terminales no son "tu turno", así que "mis-pendientes" los oculta).
//   El filtro de vista lo aplica `TablaRevision` (necesita el actor); acá viven
//   los filtros de tipo/prioridad/carrera.
// - tipo: filtra por novedad.
// - prioridad: filtra por el flag de prioritario.
// - carrera: filtra por carrera exacta (Select cerrado, no texto libre).
// ============================================================
import type { Novedad, PedidoDesignacion } from "../types";

export type VistaTablero = "mis-pendientes" | "completa";
export type FiltroTipo = "todos" | Novedad;
export type FiltroPrioridad = "todos" | "prioritarios" | "normales";

/**
 * Catálogo cerrado de carreras (D-5/D-6 de `ajustes-pedido-y-revision`): 5 carreras
 * "por ahora" según el cliente, misma fuente para el filtro Carrera y la columna
 * Carrera de la Tabla de revisión — evita que se desincronicen.
 */
export const CARRERAS: string[] = [
  "Ingeniería en Informática",
  "Ingeniería Industrial",
  "Ingeniería Civil",
  "Ingeniería Mecánica",
  "Ingeniería Electrónica",
];

/** Nombre abreviado de la carrera, para la columna Carrera de la Tabla de revisión. */
export const ABREVIATURA_CARRERA: Record<string, string> = {
  "Ingeniería en Informática": "Informática",
  "Ingeniería Industrial": "Industrial",
  "Ingeniería Civil": "Civil",
  "Ingeniería Mecánica": "Mecánica",
  "Ingeniería Electrónica": "Electrónica",
};

export interface FiltrosTablero {
  vista: VistaTablero;
  tipo: FiltroTipo;
  prioridad: FiltroPrioridad;
  carrera: string;
  nombre: string;
  legajo: string;
  /** Índice de string: permite reusar el componente genérico `FiltrosLista`. */
  [clave: string]: string;
}

export const FILTROS_INICIALES: FiltrosTablero = {
  vista: "completa",
  tipo: "todos",
  prioridad: "todos",
  carrera: "todos",
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

/** Acota los pedidos por nombre/legajo del docente, novedad, prioridad y carrera (la `vista` afecta las columnas, no las cards). */
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
    if (filtros.carrera !== "todos" && pedido.carrera !== filtros.carrera) return false;
    if (nombre && !normalizarTexto(pedido.docente.nombre).includes(nombre)) return false;
    if (legajo && !normalizarTexto(pedido.docente.legajo ?? "").includes(legajo)) return false;
    return true;
  });
}
