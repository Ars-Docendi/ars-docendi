// ============================================================
// Orden de la tabla de proyectos — lógica pura, mismo ciclo de header que `ordenTareas.ts`
// (asc → desc → sin orden manual). Sin orden manual manda el orden por defecto: primero por
// estado (en el orden del catálogo del servidor) y, dentro de cada estado, por fecha de
// inicio ascendente. Con una columna elegida, los empates se resuelven con ese mismo orden.
// ============================================================
import type { Proyecto } from "../types";

export type ColumnaOrdenableProyecto =
  "numero" | "nombre" | "responsable" | "fechaInicio" | "fechaFin" | "estado";

export interface OrdenProyectos {
  columna: ColumnaOrdenableProyecto;
  direccion: "asc" | "desc";
}

function comparar(a: string | number, b: string | number): number {
  if (typeof a === "number" && typeof b === "number") return a - b;
  return String(a).localeCompare(String(b), "es", { numeric: true });
}

/**
 * @param codigosEstado Códigos de estado en el orden del catálogo: define el rango de cada uno
 * (un estado que no esté en la lista queda al final).
 */
export function ordenarProyectos(
  proyectos: Proyecto[],
  orden: OrdenProyectos | null,
  codigosEstado: string[],
): Proyecto[] {
  const rango = (p: Proyecto) => {
    const i = codigosEstado.indexOf(p.estado);
    return i === -1 ? codigosEstado.length : i;
  };
  const porDefecto = (a: Proyecto, b: Proyecto) =>
    rango(a) - rango(b) || comparar(a.fechaInicio, b.fechaInicio) || a.numero - b.numero;

  const valor = (p: Proyecto, columna: ColumnaOrdenableProyecto): string | number => {
    switch (columna) {
      case "numero":
        return p.numero;
      case "nombre":
        return p.nombre.toLowerCase();
      case "responsable":
        return p.responsable.nombre.toLowerCase();
      case "fechaInicio":
        return p.fechaInicio;
      case "fechaFin":
        return p.fechaFin;
      case "estado":
        return rango(p);
    }
  };

  const signo = orden?.direccion === "desc" ? -1 : 1;
  return [...proyectos].sort((a, b) => {
    if (!orden) return porDefecto(a, b);
    return comparar(valor(a, orden.columna), valor(b, orden.columna)) * signo || porDefecto(a, b);
  });
}
