import { Button, InlineAlert } from "@ars-docendi/ui";

import { TablaDeResultado } from "./TablaDeResultado";
import type { EstadoDelTurno, TurnoDeLaConversacion } from "../types";

interface ContenidoHistoricoProps {
  turno: TurnoDeLaConversacion;
  onReejecutar?: (id: string) => void;
}

const TEXTO_DEL_ESTADO: Record<EstadoDelTurno, string> = {
  respondida: "Esta pregunta fue respondida.",
  no_contestable: "Esta pregunta no se pudo responder.",
  necesita_aclaracion: "Esta pregunta necesitaba una aclaración.",
  servicio_degradado: "El servicio estaba degradado cuando se hizo esta pregunta.",
};

/**
 * Un turno restaurado de una conversación reanudada (asistente-historial-conversaciones).
 *
 * NUNCA SE GUARDÓ EL TEXTO REDACTADO (design.md D2/D4): a diferencia de un
 * turno en vivo, acá no hay `respuesta.respuesta` que mostrar. Lo que se
 * muestra es el desenlace —qué pasó con esa pregunta— y, sólo si terminó
 * `respondida`, la acción «Volver a consultar», que re-ejecuta la SQL
 * guardada bajo el alcance ACTUAL del actor y nunca escribe una fila nueva de
 * historial.
 *
 * VIVE DENTRO DE `Mensaje`, que vive dentro de la región viva de
 * `Conversacion` (`role="log" aria-live="polite"`): el resultado de «Volver a
 * consultar» se anuncia solo, con el mismo mecanismo que ya usa
 * `VotoDeRetroalimentacion`, sin abrir una región propia.
 */
export function ContenidoHistorico({ turno, onReejecutar }: ContenidoHistoricoProps) {
  const historico = turno.historico;
  if (!historico) return null;

  return (
    <div className="adoc-asistente-respuesta adoc-asistente-historico">
      <span className="adoc-asistente-quien">Asistente:</span>

      <p className="adoc-asistente-texto">{TEXTO_DEL_ESTADO[historico.estado]}</p>

      {historico.sql && (
        // Solo llega con `asistente.ver_consulta` (propia) — mismo gate que
        // el turno en vivo (design.md D9).
        <details className="adoc-asistente-sql">
          <summary>Ver la consulta</summary>
          <pre>{historico.sql}</pre>
        </details>
      )}

      {historico.estado === "respondida" && (
        <div className="adoc-asistente-volver-a-consultar">
          <Button
            variant="secondary"
            size="sm"
            disabled={historico.reejecutando}
            onClick={() => onReejecutar?.(turno.id)}
          >
            Volver a consultar
          </Button>

          {historico.reejecucion &&
            (historico.reejecucion.exitosa ? (
              <>
                <p>Consulta actualizada.</p>
                <TablaDeResultado
                  columnas={historico.reejecucion.columnas}
                  filas={historico.reejecucion.filas}
                  truncado={historico.reejecucion.truncado}
                  pregunta={turno.pregunta}
                />
              </>
            ) : (
              <InlineAlert severity="info" title="No pude volver a consultar">
                {historico.reejecucion.mensaje}
              </InlineAlert>
            ))}
        </div>
      )}
    </div>
  );
}
