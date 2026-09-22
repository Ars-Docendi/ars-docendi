// ============================================================
// Filtros de la tabla de tareas — lógica pura. Mismo modelo que
// `designaciones/components/filtrosTablero.ts`: cada columna tiene su
// propio filtro en el header (`FiltroEncabezado`), no una fila de filtros
// generales aparte. Texto libre para columnas de texto/fecha; checkboxes
// (derivados de los valores presentes en las tareas, como
// `opcionesColumnasTablero`) para columnas de valores cerrados.
// ============================================================
import type { EstadoTarea, Prioridad, Tarea } from "../types";
import { formatearFecha } from "./detalleAdapters";

export interface FiltrosColumnasTareas {
  numero: string;
  titulo: string;
  autor: string[];
  responsable: string[];
  fechaInicio: string;
  fechaFin: string;
  prioridad: Prioridad[];
  avance: string;
  estado: EstadoTarea[];
}

export const FILTROS_COLUMNAS_INICIALES: FiltrosColumnasTareas = {
  numero: "",
  titulo: "",
  autor: [],
  responsable: [],
  fechaInicio: "",
  fechaFin: "",
  prioridad: [],
  avance: "",
  estado: [],
};

/** Minúsculas y sin diacríticos, para comparar sin distinguir mayúsculas/acentos. */
function normalizarTexto(texto: string): string {
  return texto
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(new RegExp("[\\u0300-\\u036f]", "g"), "");
}

function coincideTexto(valor: string, filtro: string): boolean {
  const buscado = normalizarTexto(filtro);
  return !buscado || normalizarTexto(valor).includes(buscado);
}

/** Opciones de los menús de encabezado derivadas de las tareas visibles (como en Revisión). */
export function opcionesColumnasTareas(tareas: Tarea[]) {
  return {
    autores: [...new Set(tareas.map((t) => t.creadoPor.nombre))].sort((a, b) =>
      a.localeCompare(b, "es"),
    ),
    responsables: [...new Set(tareas.map((t) => t.responsable.nombre))].sort((a, b) =>
      a.localeCompare(b, "es"),
    ),
  };
}

/** Acota las tareas por los filtros de columna activos. */
export function aplicarFiltrosColumnas(tareas: Tarea[], filtros: FiltrosColumnasTareas): Tarea[] {
  return tareas.filter((tarea) => {
    if (!coincideTexto(String(tarea.numero), filtros.numero)) return false;
    if (!coincideTexto(tarea.titulo, filtros.titulo)) return false;
    if (filtros.autor.length && !filtros.autor.includes(tarea.creadoPor.nombre)) return false;
    if (filtros.responsable.length && !filtros.responsable.includes(tarea.responsable.nombre)) {
      return false;
    }
    if (!coincideTexto(formatearFecha(tarea.fechaInicio), filtros.fechaInicio)) return false;
    if (!coincideTexto(formatearFecha(tarea.fechaFin), filtros.fechaFin)) return false;
    if (filtros.prioridad.length && !filtros.prioridad.includes(tarea.prioridad)) return false;
    if (filtros.avance !== "" && Number(filtros.avance) !== tarea.porcentajeAvance) return false;
    if (filtros.estado.length && !filtros.estado.includes(tarea.estado)) return false;
    return true;
  });
}
