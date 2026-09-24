import { useEffect, useRef, useState } from "react";
import "./MultiSelectFiltro.css";

export interface OpcionMultiSelect {
  value: string;
  label: string;
}

interface MultiSelectFiltroProps {
  /** Valores seleccionados, separados por coma. Vacío = sin filtro (todos). */
  valor: string;
  opciones: OpcionMultiSelect[];
  ariaLabel: string;
  /** Texto del botón cuando no hay nada seleccionado (por defecto "Todos"). */
  etiquetaTodos?: string;
  onChange: (valor: string) => void;
}

function csvALista(valor: string): string[] {
  return valor ? valor.split(",").filter(Boolean) : [];
}

/**
 * Filtro de selección múltiple: un botón que abre un desplegable con
 * checkboxes — a diferencia de un `<select>` nativo, permite elegir varios
 * valores a la vez (ej. "mostrar solo Pendiente y En curso"). El valor viaja
 * como string CSV para reusar el mismo `Record<string, string>` genérico de
 * `FiltrosLista`.
 */
export function MultiSelectFiltro({
  valor,
  opciones,
  ariaLabel,
  etiquetaTodos = "Todos",
  onChange,
}: MultiSelectFiltroProps) {
  const [abierto, setAbierto] = useState(false);
  const contenedorRef = useRef<HTMLDivElement>(null);
  const seleccionados = csvALista(valor);

  useEffect(() => {
    if (!abierto) return;
    function alClickearFuera(e: MouseEvent) {
      if (contenedorRef.current && !contenedorRef.current.contains(e.target as Node)) {
        setAbierto(false);
      }
    }
    document.addEventListener("mousedown", alClickearFuera);
    return () => document.removeEventListener("mousedown", alClickearFuera);
  }, [abierto]);

  function alternar(value: string) {
    const siguiente = seleccionados.includes(value)
      ? seleccionados.filter((v) => v !== value)
      : [...seleccionados, value];
    onChange(siguiente.join(","));
  }

  const etiquetaBoton =
    seleccionados.length === 0
      ? etiquetaTodos
      : seleccionados.length === 1
        ? (opciones.find((o) => o.value === seleccionados[0])?.label ?? etiquetaTodos)
        : `${seleccionados.length} seleccionados`;

  return (
    <div className="adoc-multiselect" ref={contenedorRef}>
      <button
        type="button"
        className="adoc-multiselect-boton"
        aria-haspopup="listbox"
        aria-expanded={abierto}
        aria-label={ariaLabel}
        onClick={() => setAbierto((a) => !a)}
      >
        {etiquetaBoton}
        <span className="adoc-multiselect-chevron" aria-hidden="true">
          ⌄
        </span>
      </button>
      {abierto && (
        <ul className="adoc-multiselect-lista" role="listbox" aria-multiselectable="true">
          {opciones.map((op) => (
            <li key={op.value}>
              <label className="adoc-multiselect-opcion">
                <input
                  type="checkbox"
                  checked={seleccionados.includes(op.value)}
                  onChange={() => alternar(op.value)}
                />
                {op.label}
              </label>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
