import { Mensaje } from "./Mensaje";
import type { TurnoDeLaConversacion } from "../types";

interface ConversacionProps {
  turnos: TurnoDeLaConversacion[];
  onElegir: (pregunta: string) => void;
  onReintentar: (id: string) => void;
  /** «Volver a consultar» sobre un turno histórico (asistente-historial-conversaciones). */
  onReejecutar?: (id: string) => void;
  /**
   * Edita y reenvía la última pregunta (asistente-edicion-de-la-ultima-
   * pregunta). Sólo `Mensaje` del último turno la recibe: es la única
   * pregunta que se puede reemplazar.
   */
  onEditarYReenviar?: (texto: string) => void;
  enVuelo: boolean;
  /** Cupo agotado, tope organizacional o mantenimiento: oculta «Editar y reenviar». */
  bloqueado?: boolean;
  /**
   * Anunciado por ESTA MISMA región viva: renombrar, borrar y reanudar una
   * conversación no tienen un turno propio al que colgar su confirmación, y
   * la accesibilidad de la feature pide una sola región viva, no una segunda
   * (asistente-accesibilidad). `null`/`undefined` no agrega nada al DOM.
   */
  anuncio?: string | null;
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
export function Conversacion({
  turnos,
  onElegir,
  onReintentar,
  onReejecutar,
  onEditarYReenviar,
  enVuelo,
  bloqueado = false,
  anuncio,
}: ConversacionProps) {
  return (
    <ul
      className="adoc-asistente-conversacion"
      role="log"
      aria-live="polite"
      aria-label="Conversación con el asistente"
    >
      {turnos.map((turno, indice) => {
        const esUltimo = indice === turnos.length - 1;

        return (
          <Mensaje
            // `turno.id` cambia cuando un reemplazo reenvía la pregunta con
            // una clave nueva (asistente-edicion-de-la-ultima-pregunta): con
            // una `key` distinta, React vuelve a montar este turno entero, y
            // el voto, el orden de la tabla y la vista ampliada del turno
            // reemplazado se resetean solos, sin código propio que los limpie.
            key={turno.id}
            turno={turno}
            onElegir={onElegir}
            onReintentar={onReintentar}
            onReejecutar={onReejecutar}
            onEditarYReenviar={esUltimo ? onEditarYReenviar : undefined}
            enVuelo={enVuelo}
            bloqueado={bloqueado}
            esUltimo={esUltimo}
          />
        );
      })}

      {/* El historial (renombrar, borrar, reanudar) no tiene turno propio:
          esto es lo que le da un lugar en ESTA región viva sin abrir una
          segunda. Vacío no agrega ningún nodo.

          `.adoc-sr`: en v3 cada acción anunciada ya tiene su propia
          confirmación visible en otro lado —el aviso oscuro de deshacer, los
          íconos con `aria-pressed`, las flechas de orden— así que este texto
          quedaba DUPLICADO en pantalla (asistente-rediseno-v3, hallazgo de
          verificación visual). Sigue en el DOM y en la región viva —el lector
          de pantalla lo anuncia igual—, sólo deja de pintarse. */}
      {anuncio && <li className="adoc-asistente-anuncio adoc-sr">{anuncio}</li>}
    </ul>
  );
}
