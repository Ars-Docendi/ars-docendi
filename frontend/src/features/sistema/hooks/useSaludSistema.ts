import { useQueries, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";

import {
  COMPONENTES_PING,
  comprobarPing,
  consultarEstadoSistema,
  type EstadoSistemaDto,
  type IdComponentePing,
  type ResultadoPing,
} from "../api/sistemaApi";

export const CLAVE_SALUD_PING = (id: IdComponentePing) => ["sistema", "salud", id] as const;
export const CLAVE_SALUD_POSTGRESQL = ["sistema", "salud", "postgresql"] as const;
/** Prefijo común para que `useIsFetching`/`refetchQueries` alcancen las seis consultas juntas. */
export const PREFIJO_SALUD = ["sistema", "salud"] as const;

export interface UseSaludSistemaResultado {
  pings: Record<IdComponentePing, UseQueryResult<ResultadoPing>>;
  postgresql: UseQueryResult<EstadoSistemaDto>;
  /** Reintenta un único componente (pings o `"postgresql"`), no el resto. */
  reintentar: (id: IdComponentePing | "postgresql") => void;
}

/**
 * Una React Query por componente (design D12, sistema-seccion-unificada):
 * `useQueries` en vez de un `useQuery` por sonda dentro de un `.map`, porque
 * llamar hooks adentro de un loop viola las reglas de hooks aunque la lista
 * sea de longitud fija — `useQueries` es la forma soportada por TanStack Query
 * para exactamente este caso.
 */
export function useSaludSistema(habilitado: boolean): UseSaludSistemaResultado {
  const queryClient = useQueryClient();

  const resultadosPing = useQueries({
    queries: COMPONENTES_PING.map((componente) => ({
      queryKey: CLAVE_SALUD_PING(componente.id),
      queryFn: () => comprobarPing(componente),
      enabled: habilitado,
      refetchOnWindowFocus: false,
    })),
  });

  const postgresql = useQuery({
    queryKey: CLAVE_SALUD_POSTGRESQL,
    queryFn: consultarEstadoSistema,
    enabled: habilitado,
    refetchOnWindowFocus: false,
  });

  const pings = Object.fromEntries(
    COMPONENTES_PING.map((componente, indice) => [componente.id, resultadosPing[indice]]),
  ) as Record<IdComponentePing, UseQueryResult<ResultadoPing>>;

  function reintentar(id: IdComponentePing | "postgresql") {
    const clave = id === "postgresql" ? CLAVE_SALUD_POSTGRESQL : CLAVE_SALUD_PING(id);
    void queryClient.refetchQueries({ queryKey: clave });
  }

  return { pings, postgresql, reintentar };
}
