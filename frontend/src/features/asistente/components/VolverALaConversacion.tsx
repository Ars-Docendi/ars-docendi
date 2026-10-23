import { arrowLeftIcon } from "../../../app/shell/icons";

interface VolverALaConversacionProps {
  /** Reabre el asistente, en el mismo hilo. */
  onVolver: () => void;
}

/**
 * El botón flotante que aparece en la pantalla de destino después de seguir un
 * vínculo de una respuesta (asistente-respuestas-con-vinculos, D6). Lo muestra
 * `LanzadorAsistente`, que es quien sabe por qué se cerró el asistente.
 */
export function VolverALaConversacion({ onVolver }: VolverALaConversacionProps) {
  return (
    <button type="button" className="adoc-asistente-volver" onClick={onVolver}>
      <span className="adoc-asistente-volver-icono" aria-hidden="true">
        {arrowLeftIcon}
      </span>
      Volver a la conversación
    </button>
  );
}
