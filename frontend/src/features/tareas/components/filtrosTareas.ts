// ============================================================
// Filtros del listado de tareas — lógica pura. Nro de Tarea,
// Responsable y Título siempre visibles; Autor, Estado, Prioridad,
// % Avance, Fecha Inicio y Fecha Fin se agregan bajo demanda
// ("+ Añadir filtro"). Mismo patrón que `filtrosMisPedidos.ts`.
// ============================================================
import type { Prioridad, Tarea } from "../types";

export interface FiltrosTareasState {
  numero: string;
  responsable: string;
  titulo: string;
  autor: string;
  /** Estados seleccionados, separados por coma (multi-select). Vacío = todos. */
  estado: string;
  prioridad: Prioridad | "todos";
  avance: string;
  fechaInicio: string;
  fechaFin: string;
  /** Índice de string: permite reusar el componente genérico `FiltrosLista`. */
  [clave: string]: string;
}

export const FILTROS_INICIALES: FiltrosTareasState = {
  numero: "",
  responsable: "",
  titulo: "",
  autor: "",
  estado: "",
  prioridad: "todos",
  avance: "",
  fechaInicio: "",
  fechaFin: "",
};

/** Minúsculas y sin diacríticos, para comparar sin distinguir mayúsculas/acentos. */
function normalizarTexto(texto: string): string {
  return texto
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(new RegExp("[\\u0300-\\u036f]", "g"), "");
}

/** Acota las tareas por los filtros activos. */
export function aplicarFiltrosTareas(tareas: Tarea[], filtros: FiltrosTareasState): Tarea[] {
  const numero = normalizarTexto(filtros.numero);
  const titulo = normalizarTexto(filtros.titulo);
  const autor = normalizarTexto(filtros.autor);

  return tareas.filter((tarea) => {
    if (numero && !normalizarTexto(String(tarea.numero)).includes(numero)) return false;
    if (titulo && !normalizarTexto(tarea.titulo).includes(titulo)) return false;
    if (filtros.responsable && tarea.responsable.nombre !== filtros.responsable) return false;
    if (autor && !normalizarTexto(tarea.creadoPor.nombre).includes(autor)) return false;
    if (filtros.estado) {
      const estadosSeleccionados = filtros.estado.split(",");
      if (!estadosSeleccionados.includes(tarea.estado)) return false;
    }
    if (filtros.prioridad !== "todos" && tarea.prioridad !== filtros.prioridad) return false;
    if (filtros.avance !== "" && tarea.porcentajeAvance !== Number(filtros.avance)) return false;
    if (filtros.fechaInicio && tarea.fechaInicio > filtros.fechaInicio) return false;
    if (filtros.fechaFin && tarea.fechaFin > filtros.fechaFin) return false;
    return true;
  });
}
