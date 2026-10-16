import type { ConversacionDetalle, EstadoDelTurno } from "../types";

const TEXTO_DEL_ESTADO: Record<EstadoDelTurno, string> = {
  respondida: "Respondida",
  no_contestable: "No contestable",
  necesita_aclaracion: "Necesitaba una aclaración",
  servicio_degradado: "Servicio degradado",
};

interface DetalleDeSoporteProps {
  conversacion: ConversacionDetalle;
}

/**
 * Una conversación de OTRO actor, leída por soporte
 * (asistente-acceso-de-soporte-al-historial).
 *
 * SÓLO PREGUNTA, SQL, DESENLACE Y MOMENTOS (design.md D10 de
 * asistente-historial-conversaciones) — nunca filas de resultado, y ninguna
 * acción de re-ejecución en ningún lugar de este componente. No es
 * `ContenidoHistorico`: éste no ofrece «Volver a consultar» porque esa acción
 * no existe para el historial ajeno, ni por soporte ni por nadie (design.md
 * D4/D10).
 */
export function DetalleDeSoporte({ conversacion }: DetalleDeSoporteProps) {
  return (
    <div
      className="adoc-asistente-soporte-detalle"
      aria-label={`Conversación de soporte: ${conversacion.titulo}`}
    >
      <h3>{conversacion.titulo}</h3>

      <ul className="adoc-asistente-soporte-turnos">
        {conversacion.turnos.map((turno) => (
          <li key={turno.id} className="adoc-asistente-soporte-turno">
            <p className="adoc-asistente-soporte-turno-pregunta">{turno.pregunta}</p>
            <p className="adoc-asistente-soporte-turno-estado">
              {TEXTO_DEL_ESTADO[turno.estado]} · {turno.ocurrioEn}
            </p>

            {turno.sql && (
              <details className="adoc-asistente-sql">
                <summary>Ver la consulta</summary>
                <pre>{turno.sql}</pre>
              </details>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
