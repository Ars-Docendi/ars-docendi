import type { TurnoDeLaConversacion } from "../types";

interface LineaDeMetricasProps {
  turnos: TurnoDeLaConversacion[];
}

/**
 * Lo que costó el último turno.
 *
 * VIVE FUERA DE LA REGIÓN VIVA, y no es una decisión de maquetado: cambia en cada
 * turno, así que adentro haría que el lector de pantalla la releyera junto con toda
 * la conversación cada vez. Es el defecto exacto del prototipo previo.
 */
export function LineaDeMetricas({ turnos }: LineaDeMetricasProps) {
  const ultimo = [...turnos].reverse().find((t) => t.respuesta !== undefined);

  if (!ultimo?.respuesta) return null;

  const { llamadasAlModelo } = ultimo.respuesta.metricas;

  return (
    <p className="adoc-asistente-metricas" aria-hidden="true">
      {llamadasAlModelo === 0
        ? "Resuelto sin consultar al modelo."
        : `${llamadasAlModelo} ${llamadasAlModelo === 1 ? "consulta" : "consultas"} al modelo.`}
    </p>
  );
}
