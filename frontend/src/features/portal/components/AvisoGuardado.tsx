import { useEffect } from "react";
import { Toast } from "@ars-docendi/ui";

import "./portal.css";

const DURACION_MS = 3000;

interface AvisoGuardadoProps {
  visible: boolean;
  onCerrar: () => void;
}

/**
 * Confirmación de guardado. `Toast` de la librería es solo la tarjeta visual
 * —no trae provider ni auto-dismiss—, así que el descarte automático se maneja
 * acá.
 */
export function AvisoGuardado({ visible, onCerrar }: AvisoGuardadoProps) {
  useEffect(() => {
    if (!visible) return;
    const id = window.setTimeout(onCerrar, DURACION_MS);
    return () => window.clearTimeout(id);
  }, [visible, onCerrar]);

  if (!visible) return null;

  return (
    <div className="portal-aviso">
      <Toast severity="success" title="Cambios guardados" onClose={onCerrar} />
    </div>
  );
}
