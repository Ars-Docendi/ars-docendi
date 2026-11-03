import { AyudaDelAsistente } from "./AyudaDelAsistente";

interface EncabezadoDeConversacionProps {
  /** El título de la conversación activa, o «Asistente» mientras se ve la bienvenida. */
  titulo: string;
  /**
   * Cierra el asistente. Ausente en un montaje sin noción de «cerrar» —la ruta `/asistente`,
   * mientras siga existiendo (tasks.md §10)—, y ahí el control no se pinta.
   */
  onCerrar?: () => void;
}

/**
 * El encabezado de 56 px de la columna de la conversación (asistente-superficie-frontend,
 * design.md D1 de asistente-rediseno-v3): el título de la conversación activa (o «Asistente» en la
 * bienvenida), la ayuda y, en el modal, cerrar.
 */
export function EncabezadoDeConversacion({ titulo, onCerrar }: EncabezadoDeConversacionProps) {
  return (
    <div className="adoc-asistente-encabezado">
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
