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
import type { EstadoPedido, Novedad, PedidoDesignacion } from "../types";
import { formatearFecha } from "./detalleAdapters";

export type FiltroTipo = "todos" | Novedad;
export type FiltroEstado = "todos" | Exclude<EstadoPedido, "borrador">;
export type FiltroPrioridad = "todos" | "prioritarios" | "normales";
export type FiltroSinMovimiento = "todos" | "7" | "15" | "30";

export interface FiltrosColumnasTablero {
  docente: string;
  legajo: string;
  tipo: Novedad[];
  inicio: string;
  ultima: string;
  estado: Exclude<EstadoPedido, "borrador">[];
  area: string[];
}

export const FILTROS_COLUMNAS_INICIALES: FiltrosColumnasTablero = {
  docente: "",
  legajo: "",
  tipo: [],
  inicio: "",
  ultima: "",
  estado: [],
  area: [],
};

export const OPCIONES_ESTADO: { value: FiltroEstado; label: string }[] = [
  { value: "todos", label: "Estado: Todos" },
  { value: "en_revision_coordinador", label: "En revisión · Coordinador" },
  { value: "en_revision_secretaria", label: "En revisión · Secretaría" },
  { value: "en_revision_decanato", label: "En revisión · Decanato" },
  { value: "devuelto", label: "Devuelto" },
  { value: "en_lote", label: "En lote" },
  { value: "rechazado", label: "Rechazado" },
  { value: "cancelado", label: "Cancelado" },
];

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
  estado: FiltroEstado;
  prioridad: FiltroPrioridad;
  carrera: string;
  nombre: string;
  legajo: string;
  /** Id del período de designación; "todos" = sin acotar. */
  periodo: string;
  sinMovimiento: FiltroSinMovimiento;
  /** Índice de string para reutilizar el componente de filtros generales. */
  [clave: string]: string;
}

/**
 * Período con el que abre la pantalla: el que está abierto (`activo`), del que solo
 * puede haber uno a la vez. Si no hubiera ninguno, "todos" — mejor mostrar de más que
 * esconder todo detrás de un filtro que el usuario no pidió.
 */
export const PERIODO_POR_DEFECTO = "todos";

export const FILTROS_INICIALES: FiltrosTablero = {
  tipo: "todos",
  estado: "todos",
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

function fechaInicioIso(pedido: PedidoDesignacion): string | undefined {
  return pedido.historial.find((evento) => evento.accion === "enviar")?.fecha;
}

function fechaUltimaIso(pedido: PedidoDesignacion): string | undefined {
  return pedido.historial.at(-1)?.fecha;
}

function fechaVisible(iso: string | undefined): string {
  return iso ? formatearFecha(iso) : "";
}

function areaDePedido(pedido: PedidoDesignacion): string | null {
  if (pedido.estado === "devuelto") {
    return (
      {
        "Jefe de Cátedra": "Cátedra",
        Coordinador: "Coordinación",
        Secretaría: "Secretaría",
        Decanato: "Decanato",
        Administración: "Administración",
        Docente: "el docente",
      }[pedido.propietarioActual ?? "Docente"] ?? null
    );
  }
  return (
    {
      en_revision_coordinador: "Coordinación",
      en_revision_secretaria: "Secretaría",
      en_revision_decanato: "Decanato",
    }[
      pedido.estado as "en_revision_coordinador" | "en_revision_secretaria" | "en_revision_decanato"
    ] ?? null
  );
}

function coincideFecha(iso: string | undefined, filtro: string): boolean {
  const buscado = normalizarTexto(filtro);
  return Boolean(
    buscado &&
    [iso ?? "", fechaVisible(iso)].some((valor) => normalizarTexto(valor).includes(buscado)),
  );
}

/** Opciones de los menús de encabezado derivadas de los pedidos autorizados. */
export function opcionesColumnasTablero(pedidos: PedidoDesignacion[]) {
  return {
    tipos: [...new Set(pedidos.map((pedido) => pedido.novedad))],
    estados: [
      ...new Set(pedidos.map((pedido) => pedido.estado).filter((estado) => estado !== "borrador")),
    ],
    areas: [...new Set(pedidos.map(areaDePedido).filter((area): area is string => Boolean(area)))],
  };
}

/**
 * Acota los pedidos por nombre/legajo del docente, novedad, estado, prioridad,
 * carrera, período de designación y días sin movimiento.
 */
export function aplicarFiltros(
  pedidos: PedidoDesignacion[],
  filtros: FiltrosTablero,
  columnas: FiltrosColumnasTablero = FILTROS_COLUMNAS_INICIALES,
): PedidoDesignacion[] {
  const nombre = normalizarTexto(filtros.nombre);
  const legajo = normalizarTexto(filtros.legajo);
  return pedidos.filter((pedido) => {
    if (filtros.tipo !== "todos" && pedido.novedad !== filtros.tipo) return false;
    if (filtros.estado !== "todos" && pedido.estado !== filtros.estado) return false;
    if (filtros.prioridad === "prioritarios" && !pedido.prioritario) return false;
    if (filtros.prioridad === "normales" && pedido.prioritario) return false;
    if (filtros.carrera !== "todos" && pedido.carrera !== filtros.carrera) return false;
    if (nombre && !normalizarTexto(pedido.docente.nombre).includes(nombre)) return false;
    if (legajo && !normalizarTexto(pedido.docente.legajo ?? "").includes(legajo)) return false;

    if (filtros.periodo !== "todos" && pedido.periodoId !== filtros.periodo) return false;

    if (filtros.sinMovimiento !== "todos") {
      if (diasSinMovimiento(pedido) < Number(filtros.sinMovimiento)) return false;
    }

    if (
      columnas.docente &&
      !normalizarTexto(pedido.docente.nombre).includes(normalizarTexto(columnas.docente))
    )
      return false;
    if (
      columnas.legajo &&
      !normalizarTexto(pedido.docente.legajo ?? "").includes(normalizarTexto(columnas.legajo))
    )
      return false;
    if (columnas.tipo.length && !columnas.tipo.includes(pedido.novedad)) return false;
    if (columnas.inicio && !coincideFecha(fechaInicioIso(pedido), columnas.inicio)) return false;
    if (columnas.ultima && !coincideFecha(fechaUltimaIso(pedido), columnas.ultima)) return false;
    if (
      columnas.estado.length &&
      (pedido.estado === "borrador" || !columnas.estado.includes(pedido.estado))
    )
      return false;
    if (columnas.area.length && !columnas.area.includes(areaDePedido(pedido) ?? "")) return false;

    return true;
  });
}
