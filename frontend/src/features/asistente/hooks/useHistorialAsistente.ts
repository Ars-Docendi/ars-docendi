import { useCallback, useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";

import {
  eliminarConversacion,
  eliminarTodasLasConversaciones,
  listarConversaciones,
  reanudarConversacion,
  renombrarConversacion,
} from "../api/historialApi";
import type { Asistente } from "./useAsistente";
import type { ConversacionResumen } from "../types";

const CLAVE_HISTORIAL = ["asistente", "historial"] as const;

/** Cuánto se espera desde la última tecla antes de buscar (tasks.md 10.2: debounced). */
export const ESPERA_DE_BUSQUEDA_MS = 300;

export interface HistorialAsistente {
  busqueda: string;
  setBusqueda: (valor: string) => void;
  conversaciones: UseQueryResult<ConversacionResumen[]>;
  renombrar: (id: string, titulo: string) => Promise<void>;
  eliminar: (id: string) => Promise<void>;
  eliminarTodo: () => Promise<void>;
  /** Reanuda una conversación propia. */
  abrirConversacion: (id: string) => Promise<void>;
  /**
   * El último anuncio para la región viva EXISTENTE (`Conversacion.tsx`,
   * `role="log" aria-live="polite"`) — nada de esto tiene un turno propio al
   * que colgarse, así que no hay una segunda región para esto.
   */
  anuncio: string | null;
  /**
   * La conversación activa, para resaltarla en el rail (`aria-current`) y
   * titular el encabezado (`asistente-superficie-frontend`, design.md D13).
   * La fija reanudar una conversación propia Y también la primera respuesta
   * persistida de una conversación en vivo —el rail la resalta apenas
   * aparece, sin que el usuario tenga que abrir nada—. Se limpia sola cuando
   * el hilo se vacía —«Nueva conversación» u otro reinicio—, porque ahí deja
   * de haber una fila del rail a la que corresponda seguir marcando.
   */
  conversacionActivaId: string | null;
}

/**
 * El historial propio: listar (con búsqueda), renombrar, borrar (uno o
 * todos) y reanudar. Un solo hook para los montajes que necesiten el rail
 * —el modal del lanzador, y la ruta mientras siga existiendo (tasks.md
 * §10)—, invocado por el mismo dueño que crea `asistente`.
 *
 * @param habilitado
 * Si hay que pedir la lista AHORA. El rail ya no es un cajón que se abre y
 * cierra —vive siempre montado mientras el asistente está a la vista—, así
 * que lo que decide si vale la pena pedirla es si el ASISTENTE está a la
 * vista, y eso lo sabe el dueño del montaje (el lanzador: si el modal está
 * abierto; la ruta: siempre, mientras esté montada) — no este hook.
 */
export function useHistorialAsistente(
  asistente: Asistente,
  habilitado: boolean,
): HistorialAsistente {
  const [busqueda, setBusqueda] = useState("");
  const [busquedaDebounced, setBusquedaDebounced] = useState("");
  const [anuncio, setAnuncio] = useState<string | null>(null);
  const [conversacionActivaId, setConversacionActivaId] = useState<string | null>(null);
  const cliente = useQueryClient();

  useEffect(() => {
    const temporizador = window.setTimeout(
      () => setBusquedaDebounced(busqueda),
      ESPERA_DE_BUSQUEDA_MS,
    );
    return () => window.clearTimeout(temporizador);
  }, [busqueda]);

  // AJUSTE DE ESTADO EN RENDER, no un efecto — mismo patrón que
  // `LanzadorAsistente` usa para cerrarse al navegar. La marca de «activa»
  // no sobrevive a que el hilo se vacíe: «Nueva conversación» y cualquier
  // otro reinicio dejan `turnos` en `[]`, y ahí ya no hay ninguna fila del
  // rail a la que corresponda seguir resaltando. Un `useEffect` acá pintaría
  // un frame de más con la marca vieja antes de corregirla en el siguiente
  // commit; ajustar durante el render evita ese frame extra, que es lo que
  // pide la regla `react-hooks/set-state-in-effect`.
  const [turnosVistos, setTurnosVistos] = useState(asistente.turnos.length);
  if (asistente.turnos.length !== turnosVistos) {
    setTurnosVistos(asistente.turnos.length);
    if (asistente.turnos.length === 0) setConversacionActivaId(null);
  }

  const conversaciones = useQuery({
    queryKey: [...CLAVE_HISTORIAL, busquedaDebounced || null],
    queryFn: () => listarConversaciones(busquedaDebounced || undefined),
    // Sólo mientras el asistente está a la vista: si no, no hay nada que
    // mostrar y no vale la pena pedirlo.
    enabled: habilitado,
  });

  const invalidar = useCallback(
    () => cliente.invalidateQueries({ queryKey: CLAVE_HISTORIAL }),
    [cliente],
  );

  // EL RESPONSE NOMBRA SU CONVERSACIÓN (design.md D13, tasks.md 1.2). Cada
  // turno que termina —lo haya respondido el modelo o no— pudo crear una
  // conversación nueva o tocar `ultima_actividad` de una existente, así que
  // el rail se invalida siempre. Sólo se ACTUALIZA `conversacionActivaId`
  // cuando la respuesta trae una: un turno que no se persistió (la escritura
  // falló) no tiene que pisar la conversación que ya se sabía activa.
  //
  // LA MARCA ES AJUSTE DE ESTADO EN RENDER (mismo patrón que `turnosVistos`,
  // arriba); INVALIDAR LA QUERY SIGUE EN UN EFECTO, porque sí es sincronizar
  // con un sistema externo —la caché de React Query— y no un cálculo
  // derivado del estado de React.
  const ultimaRespuesta = asistente.turnos.at(-1)?.respuesta;
  const [ultimaRespuestaVista, setUltimaRespuestaVista] = useState(ultimaRespuesta);
  if (ultimaRespuesta !== ultimaRespuestaVista) {
    setUltimaRespuestaVista(ultimaRespuesta);
    if (ultimaRespuesta?.conversacion) setConversacionActivaId(ultimaRespuesta.conversacion);
  }

  useEffect(() => {
    if (ultimaRespuesta) void invalidar();
  }, [ultimaRespuesta, invalidar]);

  const mutacionRenombrar = useMutation({
    mutationFn: ({ id, titulo }: { id: string; titulo: string }) =>
      renombrarConversacion(id, titulo),
    onSuccess: invalidar,
  });
  const mutacionEliminar = useMutation({
    mutationFn: (id: string) => eliminarConversacion(id),
    onSuccess: invalidar,
  });
  const mutacionEliminarTodo = useMutation({
    mutationFn: eliminarTodasLasConversaciones,
    onSuccess: invalidar,
  });

  return {
    busqueda,
    setBusqueda,
    conversaciones,
    anuncio,
    conversacionActivaId,
    renombrar: async (id, titulo) => {
      await mutacionRenombrar.mutateAsync({ id, titulo });
      setAnuncio("Se guardó el nuevo título.");
    },
    eliminar: async (id) => {
      await mutacionEliminar.mutateAsync(id);
      setAnuncio("Se borró la conversación.");
    },
    eliminarTodo: async () => {
      await mutacionEliminarTodo.mutateAsync();
      setAnuncio("Se borraron todas tus conversaciones.");
    },
    abrirConversacion: async (id) => {
      const { hilo, turnos } = await reanudarConversacion(id);
      asistente.sembrarDesdeHistorial(hilo, turnos);
      setConversacionActivaId(id);
      setAnuncio("La conversación está lista.");
    },
  };
}
