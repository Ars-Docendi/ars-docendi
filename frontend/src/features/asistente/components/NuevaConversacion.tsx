import { Button } from "@ars-docendi/ui";

import { plusIcon } from "../../../app/shell/icons";
import type { Asistente } from "../hooks/useAsistente";

interface NuevaConversacionProps {
  asistente: Asistente;
  /** Runs after emptying the thread (closes the phone drawer). */
  onIniciada?: () => void;
}

/** «Nueva conversación»: vacía el hilo y la próxima pregunta arranca de cero. */
export function NuevaConversacion({ asistente, onIniciada }: NuevaConversacionProps) {
  return (
    <Button
      variant="secondary"
      leadingIcon={plusIcon}
      disabled={asistente.turnos.length === 0 || asistente.enVuelo}
      onClick={() => {
        asistente.reiniciar();
        onIniciada?.();
      }}
    >
      <span className="adoc-asistente-etiqueta-boton">Nueva conversación</span>
    </Button>
  );
}
