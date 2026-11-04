import type { InfiniteData } from "@tanstack/react-query";

import type { Notificacion, PaginaNotificaciones } from "./types";

export type ListaCacheada = InfiniteData<PaginaNotificaciones, number>;

/**
 * Removes from a cached list the UNREAD notifications that match, and lowers each page's total by
 * the number removed so that "is there another page" stays coherent. Read ones never match: this
 * is the optimistic effect of marking as read on the unread tab.
 */
export function quitarNoLeidas(
  lista: ListaCacheada | undefined,
  coincide: (n: Notificacion) => boolean,
): ListaCacheada | undefined {
  if (!lista || lista.pages.length === 0) return lista;
  const quitadas = lista.pages.reduce(
    (suma, pagina) => suma + pagina.datos.filter((n) => n.leidaEn === null && coincide(n)).length,
    0,
  );
  if (quitadas === 0) return lista;
  return {
    ...lista,
    pages: lista.pages.map((pagina) => {
      const total = Math.max(0, pagina.total - quitadas);
      return {
        ...pagina,
        datos: pagina.datos.filter((n) => n.leidaEn !== null || !coincide(n)),
        total,
        totalPaginas: Math.max(1, Math.ceil(total / pagina.tamanio)),
      };
    }),
  };
}

export function decrementar(cantidad: number | undefined, por = 1): number | undefined {
  return cantidad === undefined ? cantidad : Math.max(0, cantidad - por);
}
