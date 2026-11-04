import { useQueries, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";

import {
  COMPONENTES_PING,
  comprobarPing,
  consultarEstadoInferencia,
  consultarEstadoSistema,
  type EstadoInferenciaDto,
  type EstadoSistemaDto,
  type IdComponentePing,
  type ResultadoPing,
} from "../api/sistemaApi";
import type { EntradaComponenteSalud } from "../utils/resumirSalud";

export const CLAVE_SALUD_PING = (id: IdComponentePing) => ["sistema", "salud", id] as const;
export const CLAVE_SALUD_POSTGRESQL = ["sistema", "salud", "postgresql"] as const;
export const CLAVE_SALUD_INFERENCIA = ["sistema", "salud", "inferencia"] as const;
/** Prefijo común para que `useIsFetching`/`refetchQueries` alcancen las siete consultas juntas. */
export const PREFIJO_SALUD = ["sistema", "salud"] as const;

export const NOMBRE_INFERENCIA = "Inferencia (IA)";

export type IdComponenteSalud = IdComponentePing | "postgresql" | "inferencia";

export interface UseSaludSistemaResultado {
  pings: Record<IdComponentePing, UseQueryResult<ResultadoPing>>;
  postgresql: UseQueryResult<EstadoSistemaDto>;
  inferencia: UseQueryResult<EstadoInferenciaDto>;
  /** Las siete entradas para `resumirSalud`, en el orden de las tarjetas. */
  entradas: EntradaComponenteSalud[];
  /** Reintenta un único componente (pings, `"postgresql"` o `"inferencia"`), no el resto. */
  reintentar: (id: IdComponenteSalud) => void;
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

  const inferencia = useQuery({
    queryKey: CLAVE_SALUD_INFERENCIA,
    queryFn: consultarEstadoInferencia,
    enabled: habilitado,
    refetchOnWindowFocus: false,
  });

  const pings = Object.fromEntries(
    COMPONENTES_PING.map((componente, indice) => [componente.id, resultadosPing[indice]]),
  ) as Record<IdComponentePing, UseQueryResult<ResultadoPing>>;

  const entradas: EntradaComponenteSalud[] = [
    ...COMPONENTES_PING.map((c) => ({
      id: c.id,
      nombre: c.nombre,
      disponible: pings[c.id].data?.disponible,
      comprobadoEn: pings[c.id].data?.comprobadoEn,
    })),
    {
      id: "postgresql",
      nombre: "PostgreSQL",
      disponible: postgresql.data ? postgresql.data.estado === "disponible" : undefined,
      comprobadoEn: postgresql.data?.comprobadoEn,
    },
    {
      id: "inferencia",
      nombre: NOMBRE_INFERENCIA,
      disponible: inferencia.data ? inferencia.data.estado === "disponible" : undefined,
      comprobadoEn: inferencia.data?.comprobadoEn,
      noConfigurado: inferencia.data?.estado === "deshabilitado",
    },
  ];

  function reintentar(id: IdComponenteSalud) {
    const clave =
      id === "postgresql"
        ? CLAVE_SALUD_POSTGRESQL
        : id === "inferencia"
          ? CLAVE_SALUD_INFERENCIA
          : CLAVE_SALUD_PING(id);
    void queryClient.refetchQueries({ queryKey: clave });
  }

  return { pings, postgresql, inferencia, entradas, reintentar };
}
