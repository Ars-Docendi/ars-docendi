import { useEffect, useState } from "react";

/**
 * `true` recién cuando `activo` lleva `umbralMs` seguidos; `false` en cuanto deja
 * de estarlo.
 *
 * Es el umbral del indicador de proceso, compartido con lo que lo acompaña —hoy
 * «Dejar de esperar»—: si uno aparece a los 400 ms y el otro al instante, el
 * segundo parpadea en cada respuesta determinista, que es exactamente lo que el
 * umbral evita.
 */
export function useVisibleTrasUmbral(activo: boolean, umbralMs: number): boolean {
  const [visible, setVisible] = useState(false);
  const [activoAnterior, setActivoAnterior] = useState(activo);

  // Apagarlo es un AJUSTE DE ESTADO EN RENDER y no un efecto. Bajarlo desde un
  // efecto dispara un render en cascada —React lo señala— y deja un frame con lo
  // visible todavía puesto sobre una respuesta que ya llegó.
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

  return visible;
}
