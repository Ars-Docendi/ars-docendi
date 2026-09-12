import { useCallback, useLayoutEffect, useRef, useState, type RefObject } from "react";

import type { TurnoDeLaConversacion } from "../types";

/** A cuántos píxeles del fondo, como máximo, el hilo sigue contando como anclado. */
export const UMBRAL_DE_ANCLAJE_PX = 24;

export interface AnclaAlFinal {
  /** Para el elemento que scrollea. El hook es su dueño: lo crea y lo mueve. */
  hilo: RefObject<HTMLDivElement | null>;
  /** Si el hilo está pegado abajo —o donde el panel lo dejó—. */
  anclado: boolean;
  /** Al fondo, de golpe. */
  irAlFinal: () => void;
  /** Para el `onScroll` del hilo. */
  onScroll: () => void;
}

/**
 * Sabe si el hilo está pegado abajo y lo desplaza cuando corresponde.
 *
 * TRES REGLAS, en lugar del salto ciego al fondo con cada cambio:
 * 1. Al enviar, al fondo siempre: la pregunta y el indicador tienen que verse.
 * 2. Al llegar la respuesta, si estaba anclado, al INICIO de su tarjeta y no al
 *    fondo: con una tabla larga, el fondo deja el texto arriba, fuera de vista,
 *    y el usuario ve el final de la tabla y no lo que le contestaron.
 * 3. Si el usuario subió a releer, no se lo mueve. «Ir al final» lo baja cuando
 *    él quiera.
 *
 * `anclado` no es sólo geometría. Lo que el hilo mueve por su cuenta lo deja
 * anclado —también al inicio de la tarjeta, con la tabla debajo—, y sólo un
 * scroll del usuario lo desancla, midiendo la distancia al fondo. Un `scrollTop`
 * fijado por código dispara `scroll` igual que la rueda, así que se recuerda
 * dónde se lo dejó para reconocer ese evento y no recalcular con él.
 *
 * `auto` y no `smooth` a propósito: la animación tarda, y durante ese rato el
 * texto se mueve debajo de quien está tratando de leerlo.
 */
export function useAnclaAlFinal(turnos: TurnoDeLaConversacion[]): AnclaAlFinal {
  const hilo = useRef<HTMLDivElement>(null);
  const [anclado, setAnclado] = useState(true);
  // Espejo del estado para los callbacks memoizados, que no lo ven cambiar.
  const ancladoActual = useRef(true);
  const turnosPrevios = useRef(turnos);
  // Dónde dejó al hilo el último desplazamiento propio; `null` si no hay ninguno
  // pendiente de reconocer.
  const posicionPropia = useRef<number | null>(null);

  const anclar = useCallback((valor: boolean) => {
    ancladoActual.current = valor;
    setAnclado(valor);
  }, []);

  const medir = useCallback(() => {
    const elemento = hilo.current;
    if (!elemento) return true;
    const distanciaAlFondo = elemento.scrollHeight - elemento.scrollTop - elemento.clientHeight;
    return distanciaAlFondo <= UMBRAL_DE_ANCLAJE_PX;
  }, []);

  const desplazarA = useCallback(
    (destino: number) => {
      const elemento = hilo.current;
      if (!elemento) return;
      const antes = elemento.scrollTop;
      elemento.scrollTop = destino;
      // Sólo si se movió va a haber un evento que reconocer.
      posicionPropia.current = elemento.scrollTop !== antes ? elemento.scrollTop : null;
      anclar(true);
    },
    [anclar],
  );

  const irAlFinal = useCallback(() => {
    const elemento = hilo.current;
    if (elemento) desplazarA(elemento.scrollHeight - elemento.clientHeight);
  }, [desplazarA]);

  const onScroll = useCallback(() => {
    const elemento = hilo.current;
    if (!elemento) return;

    if (posicionPropia.current !== null) {
      const esPropio = elemento.scrollTop === posicionPropia.current;
      posicionPropia.current = null;
      if (esPropio) return;
    }

    anclar(medir());
  }, [anclar, medir]);

  // Antes de pintar, para que el hilo no aparezca un instante en el lugar
  // equivocado.
  useLayoutEffect(() => {
    const previos = turnosPrevios.current;
    turnosPrevios.current = turnos;
    if (turnos === previos) return;

    // Al enviar (regla 1).
    if (turnos.length > previos.length) {
      irAlFinal();
      return;
    }

    // Se vació la conversación: se mide lo que quedó a la vista.
    if (turnos.length < previos.length) {
      anclar(medir());
      return;
    }

    // Cambió un turno: llegó su respuesta —o su error, o se lo dejó de esperar—.
    // Si el usuario subió, no se lo mueve (regla 3).
    if (!ancladoActual.current) return;

    const indice = turnos.findIndex((turno, i) => turno !== previos[i]);
    const elemento = hilo.current;
    if (indice < 0 || !elemento) return;

    // Al inicio de la tarjeta de respuesta (regla 2). Un error o un turno que se
    // dejó de esperar no la tienen; ahí el fondo alcanza, porque son cortos.
    const turno = elemento.querySelectorAll<HTMLElement>(".adoc-asistente-turno")[indice];
    const tarjeta = turno?.querySelector<HTMLElement>(".adoc-asistente-respuesta");
    if (tarjeta) desplazarA(tarjeta.offsetTop - elemento.offsetTop);
    else irAlFinal();
  }, [turnos, anclar, medir, desplazarA, irAlFinal]);

  return { hilo, anclado, irAlFinal, onScroll };
}
