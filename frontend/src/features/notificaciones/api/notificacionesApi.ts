import { apiClient } from "../../../shared/api/client";
import {
  MAXIMO_IDS_POR_PEDIDO,
  TAMANIO_PAGINA,
  type EstadoLectura,
  type Notificacion,
  type PaginaNotificaciones,
  type SolicitudMarcarLeidas,
} from "../types";

export async function contarNoLeidas(): Promise<number> {
  const { data } = await apiClient.get<{ cantidad: number }>(
    "/api/notificaciones/no-leidas/cantidad",
  );
  return data.cantidad;
}

export async function listarPagina(
  estado: EstadoLectura,
  pagina: number,
): Promise<PaginaNotificaciones> {
  const { data } = await apiClient.get<PaginaNotificaciones>("/api/notificaciones", {
    params: { pagina, tamanio: TAMANIO_PAGINA, estado },
  });
  return data;
}

export async function marcarLeida(id: string): Promise<Notificacion> {
  const { data } = await apiClient.patch<Notificacion>(`/api/notificaciones/${id}/leida`);
  return data;
}

/** Error of a chunked bulk request: which ids had already been marked before it failed. */
export class ErrorMarcadoParcial extends Error {
  completadas: string[];
  causa: unknown;

  constructor(completadas: string[], causa: unknown) {
    super("No se pudieron marcar todas las notificaciones.");
    this.completadas = completadas;
    this.causa = causa;
  }
}

/**
 * Marks several notifications. `{ todas, hasta }` is one request; explicit ids go in chunks of
 * at most 100 (the API limit), one after another, so a failure reports which chunks went through.
 */
export async function marcarLeidas(solicitud: SolicitudMarcarLeidas): Promise<number> {
  if ("todas" in solicitud) {
    const { data } = await apiClient.patch<{ cantidadActualizada: number }>(
      "/api/notificaciones/leidas",
      solicitud,
    );
    return data.cantidadActualizada;
  }
  let total = 0;
  const completadas: string[] = [];
  for (let i = 0; i < solicitud.ids.length; i += MAXIMO_IDS_POR_PEDIDO) {
    const ids = solicitud.ids.slice(i, i + MAXIMO_IDS_POR_PEDIDO);
    try {
      const { data } = await apiClient.patch<{ cantidadActualizada: number }>(
        "/api/notificaciones/leidas",
        { ids },
      );
      total += data.cantidadActualizada;
      completadas.push(...ids);
    } catch (causa) {
      throw new ErrorMarcadoParcial(completadas, causa);
    }
  }
  return total;
}
