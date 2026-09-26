import { useCallback, useEffect, useId, useRef, useState } from "react";
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
  /** Si el panel de conversaciones propias está abierto. */
  abierto: boolean;
  alternar: () => void;
  cerrar: () => void;
  busqueda: string;
  setBusqueda: (valor: string) => void;
  conversaciones: UseQueryResult<ConversacionResumen[]>;
  renombrar: (id: string, titulo: string) => Promise<void>;
  eliminar: (id: string) => Promise<void>;
  eliminarTodo: () => Promise<void>;
  /** Reanuda una conversación propia y cierra el panel. */
  abrirConversacion: (id: string) => Promise<void>;
  /**
   * El último anuncio para la región viva EXISTENTE (`Conversacion.tsx`,
   * `role="log" aria-live="polite"`) — nada de esto tiene un turno propio al
   * que colgarse, así que no hay una segunda región para esto.
   */
  anuncio: string | null;
  /**
   * La conversación reanudada más reciente, para resaltarla en la lista
   * (`aria-current`). Se limpia sola cuando el hilo se vacía —«Nueva
   * conversación» u otro reinicio—, porque ahí deja de haber una fila a la
   * que corresponda seguir marcando.
   */
  conversacionActivaId: string | null;
  /**
   * Un id ESTABLE y único por instancia de este hook, para que el botón
   * «Historial» del encabezado apunte por `aria-controls` al panel que abre
   * —viven en dos subárboles distintos del DOM desde que «Historial» se
   * mudó al encabezado—. `useId` y no una constante fija: la barra superior
   * puede tener montado el lanzador AL MISMO TIEMPO que la ruta `/asistente`
   * muestra su propio panel, y dos ids iguales en el documento son un id
   * inválido.
   */
  idDelPanel: string;
  /**
   * `AbrirHistorial` registra su botón acá (`ref={historial.registrarDisparador}`)
   * para que Escape pueda devolverle el foco al cerrar: sin esto, cerrar con
   * teclado deja el foco en el panel que se acaba de desmontar, y el
   * navegador lo manda a `<body>` — el mismo defecto que `contenedor` ya
   * evita para renombrar y borrar.
   *
   * SON DOS FUNCIONES Y NO LA REF EXPUESTA DIRECTO: una ref cruda en el
   * objeto que devuelve este hook hace que el linter (`react-hooks/refs`)
   * marque como sospechosa CUALQUIER lectura de una propiedad de
   * `historial` durante el render, no sólo la de la ref. Guardarla adentro y
   * exponer sólo funciones evita el falso positivo sin perder la
   * funcionalidad.
   */
  registrarDisparador: (elemento: HTMLButtonElement | null) => void;
  /** Enfoca el botón «Historial» ya registrado. Ver `registrarDisparador`. */
  enfocarDisparador: () => void;
}

/**
 * El historial propio: listar (con búsqueda), renombrar, borrar (uno o
 * todos) y reanudar. Un solo hook para los dos montajes —la ruta y el modal
 * del lanzador—, invocado por el MISMO dueño que crea `asistente`
 * (`AsistentePage`, `LanzadorAsistente`) y pasado como prop a
 * `AbrirHistorial` (en el encabezado) y a `PanelAsistente` (el cajón), para
 * que sean una sola cosa y no dos implementaciones que puedan
 * desincronizarse (tasks.md 10.3).
 */
export function useHistorialAsistente(asistente: Asistente): HistorialAsistente {
  const [abierto, setAbierto] = useState(false);
  const [busqueda, setBusqueda] = useState("");
  const [busquedaDebounced, setBusquedaDebounced] = useState("");
  const [anuncio, setAnuncio] = useState<string | null>(null);
  const [conversacionActivaId, setConversacionActivaId] = useState<string | null>(null);
  const idDelPanel = useId();
  const disparador = useRef<HTMLButtonElement | null>(null);
  const registrarDisparador = useCallback((elemento: HTMLButtonElement | null) => {
    disparador.current = elemento;
  }, []);
  const enfocarDisparador = useCallback(() => {
    disparador.current?.focus();
  }, []);
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
  // historial a la que corresponda seguir resaltando. Un `useEffect` acá
  // pintaría un frame de más con la marca vieja antes de corregirla en el
  // siguiente commit; ajustar durante el render evita ese frame extra, que
  // es lo que pide la regla `react-hooks/set-state-in-effect`.
  const [turnosVistos, setTurnosVistos] = useState(asistente.turnos.length);
  if (asistente.turnos.length !== turnosVistos) {
    setTurnosVistos(asistente.turnos.length);
    if (asistente.turnos.length === 0) setConversacionActivaId(null);
  }

  // ESCAPE CIERRA EL PANEL Y DEVUELVE EL FOCO A «HISTORIAL» (tasks.md §14,
  // asistente-accesibilidad). Va acá y no con `useDescartarAlClicAfuera`: ese
  // hook también cierra al clic AFUERA del panel, y el disparador que lo abre
  // vive en un subárbol distinto del DOM —el encabezado del modal o de la
  // página, no el panel—. Con ese hook, un clic en «Historial» para cerrar se
  // vería primero como un clic «afuera» en el `mousedown` y volvería a abrir
  // en el `click` que sigue: el botón dejaría de poder cerrar lo que él mismo
  // abre.
  //
  // EN FASE DE CAPTURA, y con `stopPropagation`: el panel se abre DENTRO del
  // modal del lanzador, que también cierra con Escape (`closeOnEscape` de la
  // librería, otro listener de `keydown` en `document`, en la fase normal de
  // burbuja). Sin esto, Escape con el panel abierto cerraba los dos a la vez
  // —el panel Y el modal entero—, que no es lo que alguien que sólo quería
  // cerrar la lista de conversaciones esperaba. Escuchar en captura hace que
  // esto corra primero y `stopPropagation` evita que el evento llegue a la
  // fase de burbuja, donde está el listener de la librería.
  useEffect(() => {
    if (!abierto) return;

    function alTeclear(evento: KeyboardEvent) {
      if (evento.key !== "Escape") return;
      evento.stopPropagation();
      setAbierto(false);
      disparador.current?.focus();
    }

    document.addEventListener("keydown", alTeclear, { capture: true });
    return () => document.removeEventListener("keydown", alTeclear, { capture: true });
  }, [abierto]);

  const conversaciones = useQuery({
    queryKey: [...CLAVE_HISTORIAL, busquedaDebounced || null],
    queryFn: () => listarConversaciones(busquedaDebounced || undefined),
    // Sólo mientras el panel está abierto: cerrado, no hay nada que mostrar y
    // no vale la pena pedirlo.
    enabled: abierto,
  });

  const invalidar = useCallback(
    () => cliente.invalidateQueries({ queryKey: CLAVE_HISTORIAL }),
    [cliente],
  );

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
    abierto,
    alternar: () => setAbierto((v) => !v),
    cerrar: () => setAbierto(false),
    busqueda,
    setBusqueda,
    conversaciones,
    anuncio,
    conversacionActivaId,
    idDelPanel,
    registrarDisparador,
    enfocarDisparador,
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
      setAbierto(false);
      setAnuncio("La conversación está lista.");
    },
  };
}
