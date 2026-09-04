import { useEffect, useState } from "react";

/** Cuánto se espera antes de mostrar nada, en milisegundos. */
export const UMBRAL_DE_APARICION_MS = 400;

interface IndicadorDeProcesoProps {
  activo: boolean;
  /** Inyectable para que el test no dependa del reloj real. */
  umbralMs?: number;
}

/**
 * Dice que el asistente está trabajando, pero recién después de un umbral.
 *
 * LOS TRES CARRILES TIENEN LATENCIAS QUE SE DIFERENCIAN EN UN ORDEN DE MAGNITUD: el
 * social responde al instante y el de SQL tarda segundos. Mostrar «procesando»
 * durante los milisegundos de una respuesta determinista PARPADEA, y para un lector
 * de pantalla es peor que no mostrar nada: un anuncio que aparece y desaparece antes
 * de terminar de leerse.
 *
 * NO INVENTA ETAPAS. «Interpretando… consultando… redactando…» exigiría streaming
 * del servidor, que cambia el contrato; un progreso simulado por temporizador sería
 * exactamente el fake UI que el invariante #7 prohíbe. Se muestra un solo estado
 * honesto.
 *
 * Vive FUERA de la región viva de los mensajes: es un estado, no un mensaje de la
 * conversación, y por eso lleva `role="status"` propio.
 */
export function IndicadorDeProceso({
  activo,
  umbralMs = UMBRAL_DE_APARICION_MS,
}: IndicadorDeProcesoProps) {
  const [visible, setVisible] = useState(false);
  const [activoAnterior, setActivoAnterior] = useState(activo);

  // Apagarlo es un AJUSTE DE ESTADO EN RENDER y no un efecto. Bajarlo desde un
  // efecto dispara un render en cascada —React lo señala— y, peor para lo que este
  // componente hace, deja un frame con el indicador todavía puesto sobre una
  // respuesta que ya llegó.
  if (activo !== activoAnterior) {
    setActivoAnterior(activo);
    if (!activo) setVisible(false);
  }

  // Encenderlo sí es un efecto: depende de que pase el tiempo, que es exactamente
  // el sistema externo que un efecto existe para sincronizar.
  useEffect(() => {
    if (!activo) return;

    const temporizador = window.setTimeout(() => setVisible(true), umbralMs);
    return () => window.clearTimeout(temporizador);
  }, [activo, umbralMs]);

  return (
    <div role="status" aria-live="polite" className="adoc-asistente-proceso">
      {visible ? <span>Consultando…</span> : null}
    </div>
  );
}
