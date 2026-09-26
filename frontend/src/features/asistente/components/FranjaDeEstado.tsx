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
   * Modo debug (`VITE_ASISTENTE_DEBUG=true`, ver `utils/modoDebug`): sólo con
   * esto prendido se muestra la línea de métricas (decisión 14 del PO,
   * 2026-09-26 — el mismo switch que gatea «Cómo lo interpreté» en
   * `Mensaje`). El cupo y el texto de bloqueo no dependen de esto. Parámetro,
   * no lectura directa del env, mismo motivo que en `Mensaje`.
   */
  debug?: boolean;
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
 * FUERA de la región viva del hilo, y el cupo con el mismo contrato de
 * siempre: oculto al lector donde no es texto. La línea de métricas, en
 * cambio, sólo se monta en modo debug (decisión 14 del PO).
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
