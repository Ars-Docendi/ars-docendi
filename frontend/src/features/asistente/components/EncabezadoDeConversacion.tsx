import { AyudaDelAsistente } from "./AyudaDelAsistente";

interface EncabezadoDeConversacionProps {
  /** El título de la conversación activa, o «Asistente» mientras se ve la bienvenida. */
  titulo: string;
  /**
   * Cierra el asistente. Ausente en un montaje sin noción de «cerrar» —la
   * ruta `/asistente`, mientras siga existiendo (tasks.md §10)—, y ahí el
   * control no se pinta.
   */
  onCerrar?: () => void;
}

/**
 * El encabezado de 56 px de la columna de la conversación
 * (asistente-superficie-frontend, design.md D1 de asistente-rediseno-v3):
 * el título de la conversación activa (o «Asistente» en la bienvenida), la
 * ayuda y, en el modal, cerrar.
 *
 * REEMPLAZA AL ENCABEZADO DEL `Modal` DE LA LIBRERÍA, que sigue existiendo
 * pero sin ocupar lugar (`asistente.css`, `header { height: 0 }`): la
 * librería no tiene un slot de acciones en su fila de título (TD-020), así
 * que en vez de posicionar controles ENCIMA de esa fila —como hacía antes de
 * este cambio—, este encabezado la reemplaza del todo. El diálogo sigue
 * nombrándose «Asistente» porque el `title` del `Modal` sigue montado, sólo
 * sin alto: ver `LanzadorAsistente`.
 *
 * NO ES UN `<h2>`: el título del `Modal` —sin alto pero presente en el árbol
 * de accesibilidad— ya es el `heading` que nombra al diálogo, y un segundo
 * encabezado con el mismo texto duplicaría ese nombre cuando no hay
 * conversación activa.
 *
 * LA AYUDA VA PEGADA AL TÍTULO, NO AGRUPADA CON EL CIERRE (design spec § v3):
 * `onCerrar` sólo le pone `margin-left: auto` al botón de cerrar, así que
 * empuja NADA MÁS que a sí mismo al borde derecho.
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
