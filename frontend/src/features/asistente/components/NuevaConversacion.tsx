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
 * Es el mismo botón en los dos montajes —en la ruta va en el encabezado de la
 * página, en el modal en el del panel— para que no haya dos versiones.
 *
 * SIN CONFIRMACIÓN: no hay nada persistido que perder, y una pregunta más para
 * empezar de nuevo es la fatiga de modales que los principios piden evitar. Sin
 * turnos no hay nada que vaciar; en vuelo, lo que corresponde es dejar de esperar.
 *
 * LA ETIQUETA VA EN UN `<span>` PROPIO: en el modal, a ancho de teléfono, este
 * botón comparte fila con «Historial», el título y el «?» —ya ajustada, es una
 * fila angosta— y `asistente.css` la esconde ahí (visualmente, no del árbol de
 * accesibilidad) para que el ícono solo siga alcanzando. En la ruta, donde este
 * mismo botón vive en el encabezado de la página, la regla no aplica y la
 * etiqueta se ve siempre.
 */
export function NuevaConversacion({ asistente }: NuevaConversacionProps) {
  return (
    <Button
      variant="ghost"
      size="sm"
      leadingIcon={plusIcon}
      disabled={asistente.turnos.length === 0 || asistente.enVuelo}
      onClick={asistente.reiniciar}
    >
      <span className="adoc-asistente-etiqueta-boton">Nueva conversación</span>
    </Button>
  );
}
