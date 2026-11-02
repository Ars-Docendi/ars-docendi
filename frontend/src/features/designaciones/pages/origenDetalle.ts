/** Pantalla desde la que se abrió el detalle del pedido. */
export type OrigenDetalle = "mis-pedidos" | "revision";

export interface DestinoOrigen {
  etiqueta: string;
  ruta: string;
}

const DESTINOS: Record<OrigenDetalle, DestinoOrigen> = {
  "mis-pedidos": { etiqueta: "Mis pedidos", ruta: "/designaciones/mis-pedidos" },
  revision: { etiqueta: "Revisión", ruta: "/designaciones/revision" },
};

/** Estado de navegación que Mis pedidos y Revisión pasan al abrir el detalle. */
export function estadoOrigen(origen: OrigenDetalle): { origen: OrigenDetalle } {
  return { origen };
}

function esOrigen(valor: unknown): valor is OrigenDetalle {
  return valor === "mis-pedidos" || valor === "revision";
}

/**
 * Resuelve a qué pantalla vuelve el breadcrumb del detalle. Usa el origen del estado
 * de navegación; si no viene (link directo), lo deduce por permiso para no llevar al
 * usuario a una pantalla que no puede ver.
 */
export function resolverOrigen(estado: unknown, permisos: readonly string[]): DestinoOrigen {
  const origen = (estado as { origen?: unknown } | null)?.origen;
  if (esOrigen(origen)) return DESTINOS[origen];
  return permisos.includes("designaciones.revisar") ? DESTINOS.revision : DESTINOS["mis-pedidos"];
}
