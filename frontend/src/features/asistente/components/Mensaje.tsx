import { Button, InlineAlert } from "@ars-docendi/ui";

import { AccionesDelMensaje } from "./AccionesDelMensaje";
import { Opciones } from "./Opciones";
import { Razonamiento } from "./Razonamiento";
import { Sugerencias } from "./Sugerencias";
import { TablaDeResultado } from "./TablaDeResultado";
import { hayPortapapeles } from "../utils/portapapeles";
import type { EstadoDelTurno, TurnoDeLaConversacion } from "../types";

interface MensajeProps {
  turno: TurnoDeLaConversacion;
  onElegir: (pregunta: string) => void;
  onReintentar: (id: string) => void;
  enVuelo: boolean;
}

/** Un turno completo: lo que preguntó el usuario y lo que contestó el asistente. */
export function Mensaje({ turno, onElegir, onReintentar, enVuelo }: MensajeProps) {
  const { respuesta } = turno;

  return (
    <li className="adoc-asistente-turno">
      <p className="adoc-asistente-pregunta">
        <span className="adoc-asistente-quien">Vos</span>
        {turno.pregunta}
      </p>

      {turno.error && (
        <InlineAlert severity="danger" title="No se pudo consultar">
          {turno.error}
          {/* Reusa la clave y el texto del intento, y SÓLO acá, sobre un turno que
              terminó. En vuelo o tras dejar de esperar, la misma clave haría que el
              backend ejecutara el turno entero otra vez. */}
          <div className="adoc-asistente-reintento">
            <Button
              variant="secondary"
              size="sm"
              disabled={enVuelo}
              onClick={() => onReintentar(turno.id)}
            >
              Reintentar
            </Button>
          </div>
        </InlineAlert>
      )}

      {turno.detenido && (
        // No es un error: lo pidió el usuario, y por eso no va en una alerta. Se
        // dice lo que pasó de verdad —la consulta ya salió y el backend la sigue
        // hasta el final, cupo incluido— y no se ofrece «Reintentar»: ese turno
        // sigue corriendo allá, y la misma clave lo ejecutaría dos veces.
        <p className="adoc-asistente-detenido">
          Dejaste de esperar la respuesta. La consulta ya salió y cuenta para tu cupo.
        </p>
      )}

      {respuesta && (
        <div className={`adoc-asistente-respuesta estado-${respuesta.estado}`}>
          <span className="adoc-asistente-quien">Asistente</span>

          {respuesta.preguntaInterpretada && (
            // Solo llega cuando difiere de lo que se escribió, así que mostrarla
            // nunca es ruido: es el asistente diciendo cómo entendió la pregunta.
            // Queda VISIBLE y fuera de la disclosure del razonamiento: esconder el
            // aviso de que se reinterpretó la pregunta derrota su razón de ser.
            <p className="adoc-asistente-interpretada">
              Entendí: <em>{respuesta.preguntaInterpretada}</em>
            </p>
          )}

          <MarcoDelEstado estado={respuesta.estado}>
            <p className="adoc-asistente-texto">{respuesta.respuesta}</p>
          </MarcoDelEstado>

          <TablaDeResultado
            columnas={respuesta.columnas}
            filas={respuesta.filas}
            truncado={respuesta.truncado}
          />

          <Opciones opciones={respuesta.opciones} onElegir={onElegir} deshabilitado={enVuelo} />

          <Sugerencias
            sugerencias={respuesta.sugerencias}
            onElegir={onElegir}
            deshabilitado={enVuelo}
          />

          {(respuesta.razonamiento || respuesta.sql || hayPortapapeles()) && (
            // El pie: lo que se puede desplegar a pedido y lo que se puede hacer
            // con el mensaje, después de todo lo que hay que leer. Sólo existe
            // cuando hay algo que poner: un pie vacío dejaría un hueco.
            <div className="adoc-asistente-pie">
              <Razonamiento razonamiento={respuesta.razonamiento} />

              {respuesta.sql && (
                // Solo llega con `asistente.ver_consulta`. Que esté acá no es
                // transparencia gratuita: el WHERE de una consulta generada puede
                // llevar un documento, y por eso quien la ve pasó por un permiso.
                <details className="adoc-asistente-sql">
                  <summary>Ver la consulta</summary>
                  <pre>{respuesta.sql}</pre>
                </details>
              )}

              <AccionesDelMensaje
                texto={respuesta.respuesta}
                tabla={
                  respuesta.columnas.length > 0 && respuesta.filas.length > 0
                    ? { columnas: respuesta.columnas, filas: respuesta.filas }
                    : undefined
                }
              />
            </div>
          )}
        </div>
      )}
    </li>
  );
}

/**
 * Envuelve el texto según el estado.
 *
 * EL DEGRADADO SE MUESTRA COMO ESTADO Y NO COMO ERROR. Un banner rojo le dice al
 * usuario que hizo algo mal; el servicio degradado no es culpa suya y su pregunta no
 * tiene nada de malo. La aclaración tampoco es un rechazo: es «puedo, en cuanto
 * elijas».
 */
function MarcoDelEstado({
  estado,
  children,
}: {
  estado: EstadoDelTurno;
  children: React.ReactNode;
}) {
  if (estado === "servicio_degradado") {
    return (
      <InlineAlert severity="warning" title="El asistente no está disponible ahora">
        {children}
      </InlineAlert>
    );
  }

  if (estado === "necesita_aclaracion") {
    return (
      <InlineAlert severity="info" title="Necesito que precises algo">
        {children}
      </InlineAlert>
    );
  }

  return <>{children}</>;
}
