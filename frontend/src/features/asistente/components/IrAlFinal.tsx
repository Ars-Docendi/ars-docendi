import { Button } from "@ars-docendi/ui";

import { arrowDownIcon } from "../../../app/shell/icons";

interface IrAlFinalProps {
  visible: boolean;
  onClick: () => void;
}

/**
 * «Ir al final», flotando abajo a la derecha del hilo mientras el usuario está
 * arriba. Es lo que reemplaza al hilo que arrastraba: quien subió a releer baja
 * cuando quiere, y de golpe.
 *
 * Va FUERA de la lista de mensajes: es un control del panel, no parte de la
 * conversación, y la región viva no tiene por qué anunciarlo.
 */
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
