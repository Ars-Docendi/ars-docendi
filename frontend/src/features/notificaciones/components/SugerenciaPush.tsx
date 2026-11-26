import { useState } from "react";

import { IconoCerrar } from "./iconos";

const CLAVE = "ars-docendi:notif-push-sugerencia-descartada";

function leerDescartada(): boolean {
  try {
    return localStorage.getItem(CLAVE) === "true";
  } catch {
    return false;
  }
}

/**
 * One small, dismissible line inviting to turn push on. Once dismissed it stays away in this
 * browser (best effort: without storage it only lasts while the panel is open).
 */
export function SugerenciaPush({ onActivar }: { onActivar: () => void }) {
  const [descartada, setDescartada] = useState(leerDescartada);
  if (descartada) return null;

  function descartar() {
    setDescartada(true);
    try {
      localStorage.setItem(CLAVE, "true");
    } catch {
      // Sin almacenamiento: la sugerencia vuelve la próxima vez.
    }
  }

  return (
    <div className="adoc-notif-sugerencia">
      <p>Recibí los avisos en este dispositivo, aunque no tengas la app abierta.</p>
      <button
        type="button"
        id="notif-sugerencia-activar"
        className="adoc-notif-accion"
        onClick={onActivar}
      >
        Activar
      </button>
      <button
        type="button"
        id="notif-sugerencia-descartar"
        className="adoc-notif-sugerencia-cerrar"
        aria-label="Descartar sugerencia"
        onClick={descartar}
      >
        <IconoCerrar />
      </button>
    </div>
  );
}
