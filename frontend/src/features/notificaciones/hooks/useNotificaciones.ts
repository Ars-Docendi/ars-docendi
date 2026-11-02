import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
  type QueryKey,
} from "@tanstack/react-query";

import { mostrarToast } from "../../../shared/ui/toasts";
import {
  contarNoLeidas,
  ErrorMarcadoParcial,
  listarPagina,
  marcarLeida,
  marcarLeidas,
} from "../api/notificacionesApi";
import { decrementar, quitarNoLeidas, type ListaCacheada } from "../cache";
import { NOMBRE_ENTIDAD } from "../presentacion";
import type { EstadoLectura, Notificacion, SolicitudMarcarLeidas } from "../types";

/** The count polls every 60 s and is fresh for 30 s: refocusing the window refetches only if older. */
export const INTERVALO_SONDEO_MS = 60_000;
export const VIGENCIA_MS = 30_000;

/**
 * Every key carries the active role: notifications are visible per profile, so another role is
 * another query (and never shows the previous role's cached data).
 */
export const notificacionesKeys = {
  all: ["notificaciones"] as const,
  delRol: (rol: string) => [...notificacionesKeys.all, rol] as const,
  cantidad: (rol: string) => [...notificacionesKeys.delRol(rol), "cantidad"] as const,
  listas: (rol: string) => [...notificacionesKeys.delRol(rol), "lista"] as const,
  lista: (rol: string, estado: EstadoLectura) =>
    [...notificacionesKeys.listas(rol), estado] as const,
};

/** Unread count of the bell. Runs for the whole session, panel open or not. */
export function useCantidadNoLeidas(rol: string) {
  return useQuery({
    queryKey: notificacionesKeys.cantidad(rol),
    queryFn: contarNoLeidas,
    refetchInterval: INTERVALO_SONDEO_MS,
    refetchOnWindowFocus: true,
    staleTime: VIGENCIA_MS,
    retry: 1,
  });
}

/** One tab of the panel, 20 per page. Fetched only while the panel is open on that tab. */
export function useListaNotificaciones(rol: string, estado: EstadoLectura, habilitada: boolean) {
  return useInfiniteQuery({
    queryKey: notificacionesKeys.lista(rol, estado),
    queryFn: ({ pageParam }) => listarPagina(estado, pageParam),
    initialPageParam: 1,
    getNextPageParam: (ultima) =>
      ultima.pagina < ultima.totalPaginas ? ultima.pagina + 1 : undefined,
    enabled: habilitada,
    staleTime: VIGENCIA_MS,
    retry: 1,
  });
}

// ---- Optimistic marking as read ----------------------------------------------------------
//
// This is the one place the front deliberately updates before the server answers. The rule
// "no optimism for transitions" protects confirmed business facts other people act on; reading
// a notification only changes the viewer's own state, is idempotent, and a wrong guess costs a
// rollback and a toast (see design.md, decision 7).

type Instantanea = [QueryKey, unknown][];

function tomarInstantanea(cliente: QueryClient, rol: string): Instantanea {
  return cliente.getQueriesData({ queryKey: notificacionesKeys.delRol(rol) });
}

function restaurar(cliente: QueryClient, instantanea: Instantanea) {
  for (const [clave, datos] of instantanea) cliente.setQueryData(clave, datos);
}

function quitarEnListas(cliente: QueryClient, rol: string, coincide: (n: Notificacion) => boolean) {
  cliente.setQueriesData<ListaCacheada>({ queryKey: notificacionesKeys.listas(rol) }, (lista) =>
    quitarNoLeidas(lista, coincide),
  );
}

function bajarCantidad(cliente: QueryClient, rol: string, por: number) {
  cliente.setQueryData<number>(notificacionesKeys.cantidad(rol), (cantidad) =>
    decrementar(cantidad, por),
  );
}

/** Marks one notification as read. The panel opens its entity regardless of the outcome. */
export function useMarcarLeida(rol: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (notificacion: Notificacion) => marcarLeida(notificacion.id),
    onMutate: async (notificacion) => {
      // An answer already in flight must not overwrite the optimistic state.
      await cliente.cancelQueries({ queryKey: notificacionesKeys.delRol(rol) });
      const instantanea = tomarInstantanea(cliente, rol);
      quitarEnListas(cliente, rol, (n) => n.id === notificacion.id);
      bajarCantidad(cliente, rol, 1);
      return { instantanea };
    },
    onError: (_error, notificacion, contexto) => {
      if (contexto) restaurar(cliente, contexto.instantanea);
      mostrarToast({
        titulo: "No pudimos marcar la notificación como leída",
        descripcion: `La ${NOMBRE_ENTIDAD[notificacion.tipoEntidad]} se abrió igualmente. Intentá nuevamente.`,
      });
    },
    onSettled: () => cliente.invalidateQueries({ queryKey: notificacionesKeys.delRol(rol) }),
  });
}

/** Marks several notifications as read (selection mode). */
export function useMarcarLeidas(rol: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (solicitud: SolicitudMarcarLeidas) => marcarLeidas(solicitud),
    onMutate: async (solicitud) => {
      await cliente.cancelQueries({ queryKey: notificacionesKeys.delRol(rol) });
      const instantanea = tomarInstantanea(cliente, rol);
      if ("ids" in solicitud) {
        quitarEnListas(cliente, rol, (n) => solicitud.ids.includes(n.id));
        bajarCantidad(cliente, rol, solicitud.ids.length);
      } else {
        // "todas" also covers unread rows that are not loaded: the count comes from the answer.
        const corte = Date.parse(solicitud.hasta);
        quitarEnListas(cliente, rol, (n) => Date.parse(n.creadaEn) <= corte);
      }
      return { instantanea };
    },
    onSuccess: (cantidadActualizada, solicitud) => {
      // The badge was left alone on "todas"; lower it by what the server really changed.
      if ("todas" in solicitud) bajarCantidad(cliente, rol, cantidadActualizada);
    },
    onError: (error, _solicitud, contexto) => {
      if (contexto) restaurar(cliente, contexto.instantanea);
      // Chunks that went through before the failure stay read.
      const hechas = error instanceof ErrorMarcadoParcial ? error.completadas : [];
      if (hechas.length > 0) {
        quitarEnListas(cliente, rol, (n) => hechas.includes(n.id));
        bajarCantidad(cliente, rol, hechas.length);
      }
      mostrarToast({
        titulo: "No pudimos marcar las notificaciones como leídas",
        descripcion:
          hechas.length > 0
            ? "Algunas se marcaron como leídas y otras no. Intentá nuevamente."
            : "Intentá nuevamente.",
      });
    },
    onSettled: () => cliente.invalidateQueries({ queryKey: notificacionesKeys.delRol(rol) }),
  });
}
