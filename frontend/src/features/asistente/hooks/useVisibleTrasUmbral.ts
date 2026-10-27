import { useEffect, useState } from "react";

/** `true` recién cuando `activo` lleva `umbralMs` seguidos; `false` en cuanto deja de estarlo. */
export function useVisibleTrasUmbral(activo: boolean, umbralMs: number): boolean {
  const [visible, setVisible] = useState(false);
  const [activoAnterior, setActivoAnterior] = useState(activo);

  // Apagarlo es un AJUSTE DE ESTADO EN RENDER y no un efecto.
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
