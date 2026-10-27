import { Button } from "@ars-docendi/ui";

import { arrowDownIcon } from "../../../app/shell/icons";

interface IrAlFinalProps {
  visible: boolean;
  onClick: () => void;
}

/** «Ir al final», flotando abajo a la derecha del hilo mientras el usuario está arriba. */
export function IrAlFinal({ visible, onClick }: IrAlFinalProps) {
  if (!visible) return null;

  return (
    <div className="adoc-asistente-ir-al-final">
      <Button variant="secondary" size="sm" leadingIcon={arrowDownIcon} onClick={onClick}>
        Ir al final
      </Button>
    </div>
  );
}
