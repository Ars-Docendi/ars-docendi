import { IndicadorDeCupo } from "./IndicadorDeCupo";
import { IndicadorDeProceso, UMBRAL_DE_APARICION_MS } from "./IndicadorDeProceso";
import { LineaDeMetricas } from "./LineaDeMetricas";
import { modoDebugAsistente } from "../utils/modoDebug";
import type { CupoDelActor, TurnoDeLaConversacion } from "../types";

interface FranjaDeEstadoProps {
  enVuelo: boolean;
  turnos: TurnoDeLaConversacion[];
  /** El cupo diario de `capacidades` (asistente-cupo-visible). */
  cupo?: CupoDelActor;
  /** Para el test del umbral, que no puede esperar el tiempo real. */
  umbralMs?: number;
  /**
   * Modo debug (`VITE_ASISTENTE_DEBUG=true`, ver `utils/modoDebug`): sólo con esto prendido se
   * muestra la línea de métricas (decisión 14 del PO, 2026-09-26 — el mismo switch que gatea «Cómo
   * lo interpreté» en `Mensaje`).
   */
  debug?: boolean;
}

/**
 * La fila bajo el campo de entrada: el cupo restante y el texto de bloqueo a la izquierda, lo que
 * costó el último turno a la derecha (asistente-cupo- visible, asistente-modo-mantenimiento).
 */
export function FranjaDeEstado({
  enVuelo,
  turnos,
  cupo,
  umbralMs = UMBRAL_DE_APARICION_MS,
  debug = modoDebugAsistente,
}: FranjaDeEstadoProps) {
  return (
    <div className="adoc-asistente-franja">
      <IndicadorDeProceso activo={enVuelo} umbralMs={umbralMs} />

      <IndicadorDeCupo cupo={cupo} turnos={turnos} />
      {debug && <LineaDeMetricas turnos={turnos} />}
    </div>
  );
}
