import { InlineAlert } from "@ars-docendi/ui";

import { Opciones } from "./Opciones";
import { Sugerencias } from "./Sugerencias";
import { TablaDeResultado } from "./TablaDeResultado";
import type { EstadoDelTurno, TurnoDeLaConversacion } from "../types";

interface MensajeProps {
  turno: TurnoDeLaConversacion;
  onElegir: (pregunta: string) => void;
  enVuelo: boolean;
}

/** Un turno completo: lo que preguntó el usuario y lo que contestó el asistente. */
export function Mensaje({ turno, onElegir, enVuelo }: MensajeProps) {
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
        </InlineAlert>
      )}

      {respuesta && (
        <div className={`adoc-asistente-respuesta estado-${respuesta.estado}`}>
          <span className="adoc-asistente-quien">Asistente</span>

          {respuesta.preguntaInterpretada && (
            // Solo llega cuando difiere de lo que se escribió, así que mostrarla
            // nunca es ruido: es el asistente diciendo cómo entendió la pregunta.
            <p className="adoc-asistente-interpretada">
              Lo interpreté como: <em>{respuesta.preguntaInterpretada}</em>
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

          {respuesta.sql && (
            // Solo llega con `asistente.ver_consulta`. Que esté acá no es
            // transparencia gratuita: el WHERE de una consulta generada puede
            // llevar un documento, y por eso quien la ve pasó por un permiso.
            <details className="adoc-asistente-sql">
              <summary>Ver la consulta</summary>
              <pre>{respuesta.sql}</pre>
            </details>
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
