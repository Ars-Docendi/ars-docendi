import { useVisibleTrasUmbral } from "../hooks/useVisibleTrasUmbral";

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
 * Lo que sí ocurre, y no contradice lo anterior: los turnos que no llaman al modelo
 * se retienen en el cliente para que duren como uno que sí —ver
 * `utils/esperaPareja.ts`—, así que este indicador también aparece en ellos. La
 * diferencia es de qué se afirma: acá no se dice un paso que no está pasando, sólo
 * se demora cuándo aparece una respuesta que ya está. El umbral de 400 ms sigue
 * siendo el que fija el piso de esa espera: más corta, y esto parpadearía.
 *
 * Vive FUERA de la región viva de los mensajes: es un estado, no un mensaje de la
 * conversación, y por eso lleva `role="status"` propio.
 */
export function IndicadorDeProceso({
  activo,
  umbralMs = UMBRAL_DE_APARICION_MS,
}: IndicadorDeProcesoProps) {
  const visible = useVisibleTrasUmbral(activo, umbralMs);

  return (
    <div role="status" aria-live="polite" className="adoc-asistente-proceso">
      {visible ? <span>Consultando…</span> : null}
    </div>
  );
}
