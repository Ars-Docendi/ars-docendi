import type { ReactNode } from "react";
import { Button, InlineAlert } from "@ars-docendi/ui";

interface AlertaConReintentoProps {
  titulo: string;
  children: ReactNode;
  onReintentar: () => void;
  id?: string;
}

/** Alerta de error con un "Reintentar" que es un botón real (secundario, chico), no un `<button>` suelto. */
export function AlertaConReintento({
  titulo,
  children,
  onReintentar,
  id,
}: AlertaConReintentoProps) {
  return (
    <InlineAlert severity="danger" title={titulo}>
      <div className="adoc-aulas-alerta-reintento">
        <span>{children}</span>
        <Button id={id} variant="secondary" size="sm" onClick={onReintentar}>
          Reintentar
        </Button>
      </div>
    </InlineAlert>
  );
}
