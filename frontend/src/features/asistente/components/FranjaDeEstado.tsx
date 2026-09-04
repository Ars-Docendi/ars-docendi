import { IndicadorDeProceso } from "./IndicadorDeProceso";
import { LineaDeMetricas } from "./LineaDeMetricas";
import type { TurnoDeLaConversacion } from "../types";

interface FranjaDeEstadoProps {
  enVuelo: boolean;
  turnos: TurnoDeLaConversacion[];
  /** Para el test del umbral, que no puede esperar el tiempo real. */
  umbralMs?: number;
}

/**
 * La fila entre el hilo y el campo de entrada: a la izquierda el estado del turno,
 * a la derecha lo que costó el último.
 *
 * Casi nunca se ven a la vez —el indicador mientras se espera, las métricas cuando
 * ya llegó—, así que en dos filas separadas una estaba vacía la mayor parte del
 * tiempo y el campo de entrada saltaba cada vez que la otra aparecía.
 *
 * Los dos siguen FUERA de la región viva y con el contrato de siempre: el
 * indicador es un `role="status"` con umbral y un solo texto; las métricas van
 * ocultas al lector. Cambia dónde están, no lo que anuncian.
 */
export function FranjaDeEstado({ enVuelo, turnos, umbralMs }: FranjaDeEstadoProps) {
  return (
    <div className="adoc-asistente-franja">
      <IndicadorDeProceso activo={enVuelo} umbralMs={umbralMs} />
      <LineaDeMetricas turnos={turnos} />
    </div>
  );
}
