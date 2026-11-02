import type { Notificacion, SolicitudMarcarLeidas } from "./types";

/**
 * Selection mode of the unread tab. `ids` selects the ones ticked; `todas` selects every unread
 * notification, including the ones not loaded yet (it is sent as "everything up to the newest
 * loaded instant").
 */
export interface Seleccion {
  activa: boolean;
  modo: "ids" | "todas";
  ids: string[];
}

export const SELECCION_INICIAL: Seleccion = { activa: false, modo: "ids", ids: [] };

export function iniciarSeleccion(): Seleccion {
  return { activa: true, modo: "ids", ids: [] };
}

export function estaSeleccionada(seleccion: Seleccion, id: string): boolean {
  return seleccion.modo === "todas" || seleccion.ids.includes(id);
}

/** Ticking one item while "all" is on turns it into an explicit selection minus that item. */
export function alternar(seleccion: Seleccion, id: string, cargadas: string[]): Seleccion {
  if (!seleccion.activa) return seleccion;
  const ids = seleccion.modo === "todas" ? [...cargadas] : [...seleccion.ids];
  const posicion = ids.indexOf(id);
  if (posicion >= 0) ids.splice(posicion, 1);
  else ids.push(id);
  return { activa: true, modo: "ids", ids };
}

export function cantidadSeleccionada(
  seleccion: Seleccion,
  cargadas: string[],
  totalNoLeidas: number,
): number {
  return seleccion.modo === "todas"
    ? Math.max(totalNoLeidas, cargadas.length)
    : cargadas.filter((id) => seleccion.ids.includes(id)).length;
}

export function todasSeleccionadas(
  seleccion: Seleccion,
  cargadas: string[],
  totalNoLeidas: number,
): boolean {
  return (
    seleccion.activa &&
    cargadas.length > 0 &&
    (seleccion.modo === "todas" ||
      cantidadSeleccionada(seleccion, cargadas, totalNoLeidas) >=
        Math.max(totalNoLeidas, cargadas.length))
  );
}

/** What to send for the current selection; `null` when nothing is selected. */
export function aSolicitud(
  seleccion: Seleccion,
  cargadas: Notificacion[],
): SolicitudMarcarLeidas | null {
  if (!seleccion.activa || cargadas.length === 0) return null;
  if (seleccion.modo === "todas") {
    const hasta = cargadas.reduce(
      (nuevo, n) => (Date.parse(n.creadaEn) > Date.parse(nuevo) ? n.creadaEn : nuevo),
      cargadas[0].creadaEn,
    );
    return { todas: true, hasta };
  }
  const ids = cargadas.filter((n) => seleccion.ids.includes(n.id)).map((n) => n.id);
  return ids.length > 0 ? { ids } : null;
}
