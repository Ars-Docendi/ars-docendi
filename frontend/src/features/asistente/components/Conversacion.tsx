import { Mensaje } from "./Mensaje";
import type { TurnoDeLaConversacion } from "../types";

interface ConversacionProps {
  turnos: TurnoDeLaConversacion[];
  onElegir: (pregunta: string) => void;
  onReintentar: (id: string) => void;
  enVuelo: boolean;
}

/**
 * La lista de mensajes, y NADA MÁS.
 *
 * ÉSTE ES EL DEFECTO QUE NO HAY QUE REPETIR, y está verificado en el prototipo
 * previo: la región viva envolvía el contenedor entero, así que cada re-render hacía
 * que el lector de pantalla leyera todo de nuevo — la línea de métricas incluida,
 * que cambia en cada turno.
 *
 * La región viva es exactamente esta lista. Las métricas y el indicador de proceso
 * son hermanos, fuera. `role="log"` es lo que le dice al lector que es un registro
 * de conversación donde lo nuevo se agrega al final.
 */
export function Conversacion({ turnos, onElegir, onReintentar, enVuelo }: ConversacionProps) {
  return (
    <ul
      className="adoc-asistente-conversacion"
      role="log"
      aria-live="polite"
      aria-label="Conversación con el asistente"
    >
      {turnos.map((turno) => (
        <Mensaje
          key={turno.id}
          turno={turno}
          onElegir={onElegir}
          onReintentar={onReintentar}
          enVuelo={enVuelo}
        />
      ))}
    </ul>
  );
}
