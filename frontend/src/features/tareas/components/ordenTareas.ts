// ============================================================
// Orden de la tabla de tareas — lógica pura. Mismo modelo que
// `designaciones/components/tableroRevisionModelo.ts`: `Table.HeaderCell`
// de la librería ya trae el `th` clickeable, el `aria-sort` y la flechita —
// acá solo vive el criterio. `null` = sin orden manual, y ahí manda el
// orden por defecto (Fecha Inicio ascendente); por eso el ciclo del header
// vuelve a `null` en el tercer click en vez de quedarse en asc/desc.
// ============================================================
import type { Tarea } from "../types";

export type ColumnaOrdenableTarea =
  | "numero"
  | "titulo"
  | "autor"
  | "responsable"
  | "fechaInicio"
  | "fechaFin"
  | "prioridad"
  | "avance"
  | "estado";

export interface OrdenTareas {
  columna: ColumnaOrdenableTarea;
  direccion: "asc" | "desc";
}

const RANGO_PRIORIDAD: Record<Tarea["prioridad"], number> = { baja: 1, media: 2, alta: 3 };
const RANGO_ESTADO: Record<Tarea["estado"], number> = {
  pendiente: 1,
  en_curso: 2,
  pausa: 3,
  resuelta: 4,
  cancelada: 5,
};

function valorDeOrden(tarea: Tarea, columna: ColumnaOrdenableTarea): string | number {
  switch (columna) {
    case "numero":
      return tarea.numero;
    case "titulo":
      return tarea.titulo.toLowerCase();
    case "autor":
      return tarea.creadoPor.nombre.toLowerCase();
    case "responsable":
      return tarea.responsable.nombre.toLowerCase();
    case "fechaInicio":
      return tarea.fechaInicio;
    case "fechaFin":
      return tarea.fechaFin;
    case "prioridad":
      return RANGO_PRIORIDAD[tarea.prioridad];
    case "avance":
      return tarea.porcentajeAvance;
    case "estado":
      return RANGO_ESTADO[tarea.estado];
  }
}

/** Orden por defecto cuando el usuario no eligió ninguna columna: Fecha Inicio ascendente. */
const ORDEN_POR_DEFECTO: OrdenTareas = { columna: "fechaInicio", direccion: "asc" };

/** Aplica el orden elegido; `null` aplica el orden por defecto (Fecha Inicio ascendente). */
export function ordenarTareas(tareas: Tarea[], orden: OrdenTareas | null): Tarea[] {
  const efectivo = orden ?? ORDEN_POR_DEFECTO;
  const signo = efectivo.direccion === "asc" ? 1 : -1;
  return [...tareas].sort((a, b) => {
    const va = valorDeOrden(a, efectivo.columna);
    const vb = valorDeOrden(b, efectivo.columna);
    if (typeof va === "number" && typeof vb === "number") return (va - vb) * signo;
    return String(va).localeCompare(String(vb), "es", { numeric: true }) * signo;
  });
}

/** Siguiente estado del ciclo del header: asc → desc → sin orden manual (vuelve al default). */
export function siguienteOrden(
  actual: OrdenTareas | null,
  columna: ColumnaOrdenableTarea,
): OrdenTareas | null {
  if (actual?.columna !== columna) return { columna, direccion: "asc" };
  if (actual.direccion === "asc") return { columna, direccion: "desc" };
  return null;
}
