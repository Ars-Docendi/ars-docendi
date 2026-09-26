import { IndicadorDeCupo } from "./IndicadorDeCupo";
import { IndicadorDeProceso, UMBRAL_DE_APARICION_MS } from "./IndicadorDeProceso";
import { LineaDeMetricas } from "./LineaDeMetricas";
import type { CupoDelActor, TurnoDeLaConversacion } from "../types";

interface FranjaDeEstadoProps {
  enVuelo: boolean;
  turnos: TurnoDeLaConversacion[];
  /** El cupo diario de `capacidades` (asistente-cupo-visible). */
  cupo?: CupoDelActor;
  /** Para el test del umbral, que no puede esperar el tiempo real. */
  umbralMs?: number;
}

/**
 * La fila bajo el campo de entrada: el cupo restante y el texto de bloqueo a
 * la izquierda, lo que costó el último turno a la derecha (asistente-cupo-
 * visible, asistente-modo-mantenimiento).
 *
 * «Dejar de esperar» y los puntos de «Consultando…» YA NO VIVEN ACÁ
 * (asistente-rediseno-v3, D14): el botón se mudó al lugar de «Enviar» en el
 * composer, y los puntos al lugar de la respuesta en el turno en vuelo. Lo
 * que sigue acá es el anuncio puramente accesible —`IndicadorDeProceso`,
 * ahora `sr-only`— porque el `role="status"` que lo anuncia tiene que seguir
 * FUERA de la región viva del hilo, y las métricas y el cupo con el mismo
 * contrato de siempre: ocultos al lector donde no son texto.
 */
export function FranjaDeEstado({
  enVuelo,
  turnos,
  cupo,
  umbralMs = UMBRAL_DE_APARICION_MS,
}: FranjaDeEstadoProps) {
  return (
    <div className="adoc-asistente-franja">
      <IndicadorDeProceso activo={enVuelo} umbralMs={umbralMs} />

      <IndicadorDeCupo cupo={cupo} turnos={turnos} />
      <LineaDeMetricas turnos={turnos} />
    </div>
  );
}
