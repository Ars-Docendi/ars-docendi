import type { HTMLAttributes, KeyboardEvent, ReactNode } from "react";

import "./Pestanas.css";
import { idPanelPestana, idPestana } from "./pestanasIds";

export interface ItemPestana {
  id: string;
  label: ReactNode;
  count?: number;
  disabled?: boolean;
}

interface PestanasProps extends Omit<HTMLAttributes<HTMLDivElement>, "onChange"> {
  /** Distingue los ids cuando hay más de un grupo de pestañas en la página. */
  prefijo: string;
  items: ItemPestana[];
  value: string;
  onChange: (id: string) => void;
  "aria-label": string;
}

/**
 * Pestañas del sistema de diseño (mismas clases `.adoc-tabs`) con lo que la librería no trae:
 * ids reales para `aria-controls`, Inicio/Fin además de las flechas y el foco que acompaña a la
 * selección. Las flechas ciclan, saltean las deshabilitadas y activan; sólo la pestaña activa
 * está en el orden de tabulación (tabindex itinerante).
 */
export function Pestanas({ prefijo, items, value, onChange, className, ...resto }: PestanasProps) {
  function seleccionar(id: string, destino?: HTMLElement) {
    onChange(id);
    // On phones the strip scrolls: the chosen tab ends up fully in view.
    destino?.scrollIntoView?.({ inline: "center", block: "nearest", behavior: "smooth" });
  }

  function activar(id: string) {
    onChange(id);
    // El nuevo botón ya existe: el foco acompaña a la selección.
    const boton = document.getElementById(idPestana(prefijo, id));
    boton?.focus();
    boton?.scrollIntoView?.({ inline: "center", block: "nearest" });
  }

  function alPresionar(evento: KeyboardEvent<HTMLButtonElement>, id: string) {
    const habilitadas = items.filter((item) => !item.disabled);
    const indice = habilitadas.findIndex((item) => item.id === id);
    if (indice === -1) return;
    let destino: number;
    switch (evento.key) {
      case "ArrowRight":
        destino = (indice + 1) % habilitadas.length;
        break;
      case "ArrowLeft":
        destino = (indice - 1 + habilitadas.length) % habilitadas.length;
        break;
      case "Home":
        destino = 0;
        break;
      case "End":
        destino = habilitadas.length - 1;
        break;
      default:
        return;
    }
    evento.preventDefault();
    activar(habilitadas[destino].id);
  }

  return (
    <div role="tablist" className={`adoc-tabs${className ? ` ${className}` : ""}`} {...resto}>
      {items.map((item) => {
        const activa = item.id === value;
        return (
          <button
            key={item.id}
            type="button"
            role="tab"
            id={idPestana(prefijo, item.id)}
            aria-selected={activa}
            aria-controls={idPanelPestana(prefijo, item.id)}
            tabIndex={activa ? 0 : -1}
            disabled={item.disabled}
            className={activa ? "active" : undefined}
            onClick={(evento) => seleccionar(item.id, evento.currentTarget)}
            onKeyDown={(evento) => alPresionar(evento, item.id)}
          >
            {item.label}
            {item.count != null && <span className="count">{item.count}</span>}
          </button>
        );
      })}
    </div>
  );
}

interface PanelPestanaProps extends HTMLAttributes<HTMLDivElement> {
  prefijo: string;
  /** Id de la pestaña activa a la que pertenece este panel. */
  id: string;
}

/** Panel de la pestaña activa, etiquetado por ella. */
export function PanelPestana({ prefijo, id, children, ...resto }: PanelPestanaProps) {
  return (
    <div
      role="tabpanel"
      id={idPanelPestana(prefijo, id)}
      aria-labelledby={idPestana(prefijo, id)}
      {...resto}
    >
      {children}
    </div>
  );
}
