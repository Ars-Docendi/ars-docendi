import { useRef, useState } from "react";
import type { PeriodoDesignacion } from "../types";
import "./estado-acciones.css";
import { IconoEllipsisVertical, IconoSquarePen, IconoTrash2 } from "./lucide";
import { useDescartarAlClicAfuera } from "../../../shared/hooks/useDescartarAlClicAfuera";

interface MenuAccionesPeriodoProps {
  periodo: PeriodoDesignacion;
  onEditar: (periodo: PeriodoDesignacion) => void;
  onEliminar: (periodo: PeriodoDesignacion) => void;
}

/** Menú kebab (⋮) con las acciones del período: Editar y Eliminar. */
export function MenuAccionesPeriodo({ periodo, onEditar, onEliminar }: MenuAccionesPeriodoProps) {
  const [abierto, setAbierto] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useDescartarAlClicAfuera(abierto, ref, () => setAbierto(false));

  function ejecutar(accion: (periodo: PeriodoDesignacion) => void) {
    setAbierto(false);
    accion(periodo);
  }

  return (
    <div className="adoc-menu-acc" ref={ref}>
      <button
        type="button"
        className="adoc-menu-acc-trigger bordeado"
        aria-label={`Acciones del período ${periodo.nombre}`}
        aria-haspopup="menu"
        aria-expanded={abierto}
        onClick={() => setAbierto((o) => !o)}
      >
        <IconoEllipsisVertical />
      </button>
      {abierto && (
        <div className="adoc-menu-acc-pop" role="menu">
          <button type="button" role="menuitem" onClick={() => ejecutar(onEditar)}>
            <IconoSquarePen /> Editar
          </button>
          <button
            type="button"
            role="menuitem"
            className="peligro"
            onClick={() => ejecutar(onEliminar)}
          >
            <IconoTrash2 /> Eliminar
          </button>
        </div>
      )}
    </div>
  );
}
