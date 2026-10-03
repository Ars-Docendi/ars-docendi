import { useCallback, useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";

import {
  archivarConversacion,
  desarchivarConversacion,
  deshacerBorrado,
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

/**
 * Cuánto se muestra el aviso de deshacer, en milisegundos
 * (asistente-superficie-frontend, design.md D4/D15 de asistente-rediseno-v3).
 *
 * SON 10 s ACÁ Y 15 EN EL SERVIDOR, A PROPÓSITO: el servidor da 5 s de
 * margen de red por encima de lo que la interfaz muestra, para que un clic a
 * los 9,9 s con una conexión lenta todavía llegue a tiempo. Los dos números
 * viven en archivos distintos porque describen cosas distintas — cuánto
 * dura el aviso en pantalla, y hasta cuándo el servidor lo acepta — y no
 * porque se hayan desincronizado.
 */
export const DURACION_DEL_AVISO_MS = 10_000;

/** El aviso de deshacer al pie del rail. Uno a la vez. */
export interface AvisoDeDeshacer {
  texto: string;
  deshacer: () => Promise<void>;
}

export interface HistorialAsistente {
  busqueda: string;
  setBusqueda: (valor: string) => void;
  conversaciones: UseQueryResult<ConversacionResumen[]>;
  renombrar: (id: string, titulo: string) => Promise<void>;
  /** Archiva una conversación propia (design.md D3). */
  archivar: (id: string) => Promise<void>;
  /** Desarchiva una conversación propia. */
  desarchivar: (id: string) => Promise<void>;
  /**
   * Marca una conversación propia pendiente de borrado. SIN CONFIRMACIÓN
   * (asistente-superficie-frontend): la fila desaparece de inmediato y el
   * aviso de deshacer es la red de seguridad, no un diálogo previo.
   */
  eliminar: (id: string) => Promise<void>;
  /** Marca TODAS las conversaciones propias (archivadas incluidas) pendientes de borrado. */
  eliminarTodo: () => Promise<void>;
  /** Reanuda una conversación propia. */
  abrirConversacion: (id: string) => Promise<void>;
  /**
   * El aviso de deshacer vigente, o `null` sin ninguno. Se vence solo a los
   * `DURACION_DEL_AVISO_MS`; una acción nueva lo reemplaza (nunca conviven
   * dos).
   */
  aviso: AvisoDeDeshacer | null;
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
 * El historial propio: listar (con búsqueda), renombrar, archivar/desarchivar,
 * borrar (uno o todos, diferido y deshacible) y reanudar. Lo invoca el mismo
 * dueño que crea `asistente`: el lanzador de la barra, único montaje real desde
 * ARS-151 (tasks.md §10). `PanelDePrueba` también lo invoca, pero sólo para
 * probar `PanelAsistente` en aislamiento — no es un segundo montaje de
 * producción.
 *
 * @param habilitado
 * Si hay que pedir la lista AHORA. El rail ya no es un cajón que se abre y
 * cierra —vive siempre montado mientras el asistente está a la vista—, así
 * que lo que decide si vale la pena pedirla es si el ASISTENTE está a la
 * vista, y eso lo sabe el dueño del montaje (el lanzador: si el modal está
 * abierto) — no este hook.
 */
export function useHistorialAsistente(
  asistente: Asistente,
  habilitado: boolean,
): HistorialAsistente {
  const [busqueda, setBusqueda] = useState("");
  const [busquedaDebounced, setBusquedaDebounced] = useState("");
  const [anuncio, setAnuncio] = useState<string | null>(null);
  const [conversacionActivaId, setConversacionActivaId] = useState<string | null>(null);
  const [aviso, setAviso] = useState<AvisoDeDeshacer | null>(null);
  const cliente = useQueryClient();

  useEffect(() => {
    const temporizador = window.setTimeout(
      () => setBusquedaDebounced(busqueda),
      ESPERA_DE_BUSQUEDA_MS,
    );
    return () => window.clearTimeout(temporizador);
  }, [busqueda]);

  // EL AVISO SE VENCE SOLO. Atado a la IDENTIDAD del objeto `aviso` y no a
  // su texto: una acción nueva siempre crea un objeto nuevo —incluso con el
  // mismo texto—, así que este efecto limpia el temporizador viejo y arranca
  // uno propio, que es exactamente «una acción nueva reemplaza al aviso
  // anterior» (asistente-superficie-frontend).
  useEffect(() => {
    if (!aviso) return;
    const temporizador = window.setTimeout(() => setAviso(null), DURACION_DEL_AVISO_MS);
    return () => window.clearTimeout(temporizador);
  }, [aviso]);

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
  const mutacionArchivar = useMutation({
    mutationFn: (id: string) => archivarConversacion(id),
    onSuccess: invalidar,
  });
  const mutacionDesarchivar = useMutation({
    mutationFn: (id: string) => desarchivarConversacion(id),
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
  const mutacionDeshacer = useMutation({
    mutationFn: (lote: string) => deshacerBorrado(lote),
    onSuccess: invalidar,
  });

  /**
   * Reanuda una conversación propia — la misma acción que `abrirConversacion`
   * expone hacia afuera, pero también usada internamente para «Deshacer»
   * cuando la conversación archivada/borrada era la activa (asistente-superficie-frontend:
   * «Deshacer» la reanuda de nuevo).
   */
  const resumir = useCallback(
    async (id: string) => {
      const { hilo, turnos } = await reanudarConversacion(id);
      asistente.sembrarDesdeHistorial(hilo, turnos);
      setConversacionActivaId(id);
    },
    [asistente],
  );

  /**
   * Si `id` es la conversación activa, vuelve a la bienvenida
   * (asistente-superficie-frontend: archivar o eliminar la conversación
   * activa resetea el hilo) y devuelve si lo hizo — lo que necesita el
   * llamador para saber si «Deshacer» tiene que volver a reanudarla.
   */
  function soltarSiEsLaActiva(id: string): boolean {
    if (id !== conversacionActivaId) return false;
    asistente.reiniciar();
    setConversacionActivaId(null);
    return true;
  }

  async function reanudarSiEraLaActiva(id: string, eraActiva: boolean) {
    if (eraActiva) await resumir(id);
  }

  return {
    busqueda,
    setBusqueda,
    conversaciones,
    aviso,
    anuncio,
    conversacionActivaId,
    renombrar: async (id, titulo) => {
      await mutacionRenombrar.mutateAsync({ id, titulo });
      setAnuncio("Se guardó el nuevo título.");
    },
    archivar: async (id) => {
      const eraActiva = soltarSiEsLaActiva(id);
      await mutacionArchivar.mutateAsync(id);
      setAnuncio("Se archivó la conversación. Podés deshacerlo durante 10 segundos.");
      setAviso({
        texto: "Conversación archivada",
        deshacer: async () => {
          await mutacionDesarchivar.mutateAsync(id);
          await reanudarSiEraLaActiva(id, eraActiva);
          setAnuncio("Se restauró la conversación.");
        },
      });
    },
    desarchivar: async (id) => {
      await mutacionDesarchivar.mutateAsync(id);
      setAnuncio("Se restauró la conversación. Podés deshacerlo durante 10 segundos.");
      setAviso({
        texto: "Conversación restaurada",
        deshacer: async () => {
          await mutacionArchivar.mutateAsync(id);
          setAnuncio("Se archivó la conversación.");
        },
      });
    },
    eliminar: async (id) => {
      const eraActiva = soltarSiEsLaActiva(id);
      const { loteDeBorrado } = await mutacionEliminar.mutateAsync(id);
      setAnuncio("Se eliminó la conversación. Podés deshacerlo durante 10 segundos.");
      setAviso({
        texto: "Conversación eliminada",
        deshacer: async () => {
          await mutacionDeshacer.mutateAsync(loteDeBorrado);
          await reanudarSiEraLaActiva(id, eraActiva);
          setAnuncio("Se restauró la conversación.");
        },
      });
    },
    eliminarTodo: async () => {
      const idActivaAntes = conversacionActivaId;
      if (idActivaAntes) {
        asistente.reiniciar();
        setConversacionActivaId(null);
      }
      const { loteDeBorrado } = await mutacionEliminarTodo.mutateAsync();
      setAnuncio("Se eliminaron todas tus conversaciones. Podés deshacerlo durante 10 segundos.");
      setAviso({
        texto: "Conversaciones eliminadas",
        deshacer: async () => {
          await mutacionDeshacer.mutateAsync(loteDeBorrado);
          if (idActivaAntes) await resumir(idActivaAntes);
          setAnuncio("Se restauraron tus conversaciones.");
        },
      });
    },
    abrirConversacion: async (id) => {
      await resumir(id);
      setAnuncio("La conversación está lista.");
    },
  };
}
