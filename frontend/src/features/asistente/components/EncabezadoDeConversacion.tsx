import type { Ref } from "react";

import { AyudaDelAsistente } from "./AyudaDelAsistente";
import { historyIcon } from "../../../app/shell/icons";

interface EncabezadoDeConversacionProps {
  /** El título de la conversación activa, o «Asistente» mientras se ve la bienvenida. */
  titulo: string;
  /**
   * Cierra el asistente. Ausente en un montaje sin noción de «cerrar» —la ruta `/asistente`,
   * mientras siga existiendo (tasks.md §10)—, y ahí el control no se pinta.
   */
  onCerrar?: () => void;
  /** Phones: opens the conversations drawer (the rail is not a column there). */
  conversaciones?: { abiertas: boolean; onAbrir: () => void };
  /** The drawer button, so closing the drawer can return the focus to it. */
  refConversaciones?: Ref<HTMLButtonElement>;
}

/**
 * El encabezado de 56 px de la columna de la conversación (asistente-superficie-frontend,
 * design.md D1 de asistente-rediseno-v3): el título de la conversación activa (o «Asistente» en la
 * bienvenida), la ayuda y, en el modal, cerrar.
 */
export function EncabezadoDeConversacion({
  titulo,
  onCerrar,
  conversaciones,
  refConversaciones,
}: EncabezadoDeConversacionProps) {
  return (
    <div className="adoc-asistente-encabezado">
      {conversaciones && (
        <button
          ref={refConversaciones}
          type="button"
          className="adoc-asistente-encabezado-conversaciones"
          aria-label="Conversaciones"
          aria-expanded={conversaciones.abiertas}
          aria-controls="asistente-conversaciones"
          onClick={conversaciones.onAbrir}
        >
          <span className="ico" aria-hidden="true">
            {historyIcon}
          </span>
        </button>
      )}
      <p className="adoc-asistente-encabezado-titulo" title={titulo}>
        {titulo}
      </p>
      <AyudaDelAsistente />
      {onCerrar && (
        <button
          type="button"
          className="adoc-asistente-encabezado-cerrar"
          aria-label="Cerrar"
          onClick={onCerrar}
        >
          ×
        </button>
      )}
    </div>
  );
}
