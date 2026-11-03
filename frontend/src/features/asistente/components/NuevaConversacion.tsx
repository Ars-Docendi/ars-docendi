import { Button } from "@ars-docendi/ui";

import { plusIcon } from "../../../app/shell/icons";
import type { Asistente } from "../hooks/useAsistente";

interface NuevaConversacionProps {
  asistente: Asistente;
}

/** «Nueva conversación»: vacía el hilo y la próxima pregunta arranca de cero. */
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
