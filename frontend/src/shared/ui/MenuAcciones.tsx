import { useEffect, useRef, useState, type ReactNode } from "react";

import "./MenuAcciones.css";
import { IconoEllipsisVertical } from "./iconos";

export interface AccionMenu {
  etiqueta: string;
  icono?: ReactNode;
  /** Pinta la acción como destructiva. */
  peligro?: boolean;
  onSelect: () => void;
}

interface MenuAccionesProps {
  acciones: AccionMenu[];
  /** Etiqueta accesible del disparador, p. ej. "Acciones de Bases de datos". */
  etiquetaAria: string;
}

/** Menú kebab (⋮) genérico de acciones sobre un ítem de una lista. */
export function MenuAcciones({ acciones, etiquetaAria }: MenuAccionesProps) {
  const [abierto, setAbierto] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!abierto) return;
    function onPointer(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setAbierto(false);
    }
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") setAbierto(false);
    }
    document.addEventListener("mousedown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [abierto]);

  function ejecutar(accion: AccionMenu) {
    setAbierto(false);
    accion.onSelect();
  }

  return (
    <div className="adoc-acciones" ref={ref}>
      <button
        type="button"
        className="adoc-acciones-trigger"
        aria-label={etiquetaAria}
        aria-haspopup="menu"
        aria-expanded={abierto}
        onClick={() => setAbierto((o) => !o)}
      >
        <IconoEllipsisVertical />
      </button>
      {abierto && (
        <div className="adoc-acciones-pop" role="menu">
          {acciones.map((accion) => (
            <button
              key={accion.etiqueta}
              type="button"
              role="menuitem"
              className={accion.peligro ? "peligro" : undefined}
              onClick={() => ejecutar(accion)}
            >
              {accion.icono}
              {accion.etiqueta}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
