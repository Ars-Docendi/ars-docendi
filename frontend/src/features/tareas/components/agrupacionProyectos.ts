// ============================================================
// Agrupación de tareas por Proyecto para la pantalla inicial —
// LÓGICA PURA. "Generales" (tareas sin proyecto) siempre primero
// (cuadro fijo, aparece aunque esté vacío); luego un cuadro por cada
// Proyecto en estado Abierto que tenga al menos una tarea, ordenados
// por Fecha de Fin del Proyecto, de la más reciente a la más
// antigua. Los Proyectos Finalizados/Cancelados no generan cuadro
// (ver `specs/proyectos/spec.md`, Requirement "Acceso manual a
// Proyectos Finalizados o Cancelados").
// ============================================================
import type { Proyecto, Tarea } from "../types";

export interface CuadroProyectoAgrupado {
  /** `null` representa el cuadro fijo "Generales" (tareas sin proyecto). */
  proyecto: Proyecto | null;
  tareas: Tarea[];
}

export function agruparTareasPorProyecto(
  tareas: Tarea[],
  proyectos: Proyecto[],
): CuadroProyectoAgrupado[] {
  const generales: CuadroProyectoAgrupado = {
    proyecto: null,
    tareas: tareas.filter((t) => !t.proyectoId),
  };

  const cuadrosDeProyecto = proyectos
    .filter((p) => p.estado === "abierto")
    .map((proyecto) => ({
      proyecto,
      tareas: tareas.filter((t) => t.proyectoId === proyecto.id),
    }))
    .filter((cuadro) => cuadro.tareas.length > 0)
    .sort((a, b) => b.proyecto.fechaFin.localeCompare(a.proyecto.fechaFin));

  return [generales, ...cuadrosDeProyecto];
}
