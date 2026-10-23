import { Button } from "@ars-docendi/ui";

import { plusIcon } from "../../../app/shell/icons";
import type { Asistente } from "../hooks/useAsistente";

interface NuevaConversacionProps {
  asistente: Asistente;
}

/**
 * «Nueva conversación»: vacía el hilo y la próxima pregunta arranca de cero. El
 * backend acepta un hilo nulo como conversación nueva, así que es real.
 *
 * Vive en el rail (`RailDeConversaciones`), el mismo componente expandido y
 * colapsado, para que no haya dos versiones.
 *
 * SIN CONFIRMACIÓN: no hay nada persistido que perder, y una pregunta más para
 * empezar de nuevo es la fatiga de modales que los principios piden evitar. Sin
 * turnos no hay nada que vaciar; en vuelo, lo que corresponde es dejar de esperar.
 *
 * `variant="secondary"` Y SIN `size`, a propósito: es el tamaño BASE de la
 * librería (36 px / 14px / 500, borde fuerte, fondo blanco) — exactamente lo
 * que pide design spec § v3 para el botón de ancho completo del rail
 * expandido. `asistente.css` lo reduce a un ícono cuadrado cuando el rail
 * está colapsado, y neutraliza el aspecto «gris» de `:disabled` de la
 * librería para que siga leyéndose como el mismo botón sin turnos.
 *
 * LA ETIQUETA VA EN UN `<span>` PROPIO: colapsado, `asistente.css` la
 * esconde visualmente (no del árbol de accesibilidad) para que el ícono
 * solo siga alcanzando; expandido, se ve siempre.
 */
export function NuevaConversacion({ asistente }: NuevaConversacionProps) {
  return (
    <Button
      variant="secondary"
      leadingIcon={plusIcon}
      disabled={asistente.turnos.length === 0 || asistente.enVuelo}
      onClick={asistente.reiniciar}
    >
      <span className="adoc-asistente-etiqueta-boton">Nueva conversación</span>
    </Button>
  );
}
