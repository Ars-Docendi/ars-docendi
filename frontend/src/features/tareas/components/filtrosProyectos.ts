// ============================================================
// Filtros de la tabla de proyectos — lógica pura, mismo modelo que `filtrosTareas.ts`:
// un filtro por columna en su propio header. Texto libre para las columnas de texto y de
// fecha; checkboxes para Responsable y Estado.
// ============================================================
import type { Proyecto } from "../types";
import { formatearFecha } from "./detalleAdapters";
import { coincideTexto } from "./filtrosTareas";

export interface FiltrosColumnasProyectos {
  numero: string;
  nombre: string;
  responsable: string[];
  fechaInicio: string;
  fechaFin: string;
  estado: string[];
}

export const FILTROS_PROYECTOS_INICIALES: FiltrosColumnasProyectos = {
  numero: "",
  nombre: "",
  responsable: [],
  fechaInicio: "",
  fechaFin: "",
  estado: [],
};

/** Responsables presentes en los proyectos, ordenados, para el menú del encabezado. */
export function responsablesDeProyectos(proyectos: Proyecto[]): string[] {
  return [...new Set(proyectos.map((p) => p.responsable.nombre))].sort((a, b) =>
    a.localeCompare(b, "es"),
  );
}

/** Acota los proyectos por los filtros de columna activos. */
export function aplicarFiltrosProyectos(
  proyectos: Proyecto[],
  filtros: FiltrosColumnasProyectos,
): Proyecto[] {
  return proyectos.filter((proyecto) => {
    if (!coincideTexto(String(proyecto.numero), filtros.numero)) return false;
    if (!coincideTexto(proyecto.nombre, filtros.nombre)) return false;
    if (filtros.responsable.length && !filtros.responsable.includes(proyecto.responsable.nombre)) {
      return false;
    }
    if (!coincideTexto(formatearFecha(proyecto.fechaInicio), filtros.fechaInicio)) return false;
    if (!coincideTexto(formatearFecha(proyecto.fechaFin), filtros.fechaFin)) return false;
    if (filtros.estado.length && !filtros.estado.includes(proyecto.estado)) return false;
    return true;
  });
}
