/** Constructores de URL del Centro de ayuda. */

/** Slug que no puede usar ningún módulo: `/ayuda/buscar` es la página de resultados. */
export const SLUG_RESERVADO = "buscar";

export const RUTA_AYUDA = "/ayuda";

export const ID_PREGUNTAS_FRECUENTES = "preguntas-frecuentes";

export function rutaModulo(moduloSlug: string): string {
  return `${RUTA_AYUDA}/${moduloSlug}`;
}

export function rutaArticulo(moduloSlug: string, articuloSlug: string): string {
  return `${rutaModulo(moduloSlug)}/${articuloSlug}`;
}

/** Id del elemento HTML del paso `numero` (base 1). */
export function idPaso(numero: number): string {
  return `paso-${numero}`;
}

export function rutaPaso(moduloSlug: string, articuloSlug: string, numero: number): string {
  return `${rutaArticulo(moduloSlug, articuloSlug)}#${idPaso(numero)}`;
}

export function rutaBusqueda(consulta: string): string {
  return `${RUTA_AYUDA}/${SLUG_RESERVADO}?q=${encodeURIComponent(consulta)}`;
}
