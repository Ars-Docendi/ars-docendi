// ============================================================
// Filtros de la Tabla de revisión. Lógica pura.
// El viejo filtro `vista` ("completa" | "mis-pendientes") ya no vive acá: lo
// reemplazó la pestaña "Mi bandeja" de la Tabla, que hace lo mismo pero con el
// conteo a la vista y sin competir con las otras etapas por el mismo control.
// - tipo: filtra por novedad.
// - periodo: acota por el período de designación del pedido (`periodoId`). Responde
//   "¿qué entró en tal período?" mejor que un rango de fechas libre: los períodos son
//   una entidad del dominio, ya están creados y nombrados ("1er cuatrimestre 2026"),
//   y el pedido ya los referencia — no hay que adivinar fechas de corte. Arranca en el
//   período ABIERTO, no en "todos": un revisor trabaja sobre el período en curso, y
//   mezclarle las designaciones de cuatrimestres cerrados es ruido.
// - sinMovimiento: días mínimos sin que el pedido se mueva, sobre la fecha del último
//   evento. Responde "¿qué está trabado?" — reemplaza al contador de días por fila que
//   el cliente pidió sacar: en vez de que cada fila grite el número, se pregunta por
//   los que pasan un umbral.
// - prioridad: filtra por el flag de prioritario.
// - carrera: filtra por carrera exacta (Select cerrado, no texto libre).
// ============================================================
import type { Novedad, PedidoDesignacion } from "../types";
import { PERIODOS_MOCK } from "../api/periodosMock";

export type FiltroTipo = "todos" | Novedad;
export type FiltroPrioridad = "todos" | "prioritarios" | "normales";
export type FiltroSinMovimiento = "todos" | "7" | "15" | "30";

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
  tipo: FiltroTipo;
  prioridad: FiltroPrioridad;
  carrera: string;
  nombre: string;
  legajo: string;
  /** Id del período de designación; "todos" = sin acotar. */
  periodo: string;
  sinMovimiento: FiltroSinMovimiento;
  /** Índice de string: permite reusar el componente genérico `FiltrosLista`. */
  [clave: string]: string;
}

/**
 * Período con el que abre la pantalla: el que está abierto (`activo`), del que solo
 * puede haber uno a la vez. Si no hubiera ninguno, "todos" — mejor mostrar de más que
 * esconder todo detrás de un filtro que el usuario no pidió.
 */
export const PERIODO_POR_DEFECTO: string =
  PERIODOS_MOCK.find((periodo) => periodo.activo)?.id ?? "todos";

export const FILTROS_INICIALES: FiltrosTablero = {
  tipo: "todos",
  prioridad: "todos",
  carrera: "todos",
  nombre: "",
  legajo: "",
  periodo: PERIODO_POR_DEFECTO,
  sinMovimiento: "todos",
};

/** Minúsculas y sin diacríticos, para comparar sin distinguir mayúsculas/acentos. */
function normalizarTexto(texto: string): string {
  return texto
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(new RegExp("[\\u0300-\\u036f]", "g"), "");
}

const MS_POR_DIA = 24 * 60 * 60 * 1000;

/** Días enteros que el pedido lleva sin moverse (desde su último evento). */
function diasSinMovimiento(pedido: PedidoDesignacion): number {
  const iso = pedido.historial.at(-1)?.fecha;
  if (!iso) return 0;
  return Math.floor((Date.now() - new Date(iso).getTime()) / MS_POR_DIA);
}

/**
 * Acota los pedidos por nombre/legajo del docente, novedad, prioridad, carrera,
 * período de designación y días sin movimiento.
 */
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

    if (filtros.periodo !== "todos" && pedido.periodoId !== filtros.periodo) return false;

    if (filtros.sinMovimiento !== "todos") {
      if (diasSinMovimiento(pedido) < Number(filtros.sinMovimiento)) return false;
    }

    return true;
  });
}
