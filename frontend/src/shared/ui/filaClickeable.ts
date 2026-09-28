import type { KeyboardEvent, MouseEvent } from "react";
import "./filaClickeable.css";

const INTERACTIVOS = "button, a, input, select, textarea, label, [role='menuitem']";

/**
 * Props para que toda la fila ejecute su acción principal (ver o editar), con el
 * mouse o con el teclado (Tab para enfocarla, Enter o Espacio para ejecutarla).
 * Sin acción —el usuario no puede ejecutarla— la fila no es clickeable. Los
 * controles de la fila hacen lo suyo y seleccionar texto no dispara nada.
 */
export function propsFilaClickeable(accion?: () => void) {
  if (!accion) return {};
  return {
    className: "adoc-fila-clickeable",
    tabIndex: 0,
    onClick: (evento: MouseEvent<HTMLElement>) => {
      if ((evento.target as HTMLElement).closest(INTERACTIVOS)) return;
      if (window.getSelection()?.toString()) return;
      accion();
    },
    onKeyDown: (evento: KeyboardEvent<HTMLElement>) => {
      // Solo con el foco en la fila: Enter sobre un botón de adentro ejecuta ese botón.
      if (evento.target !== evento.currentTarget) return;
      if (evento.key !== "Enter" && evento.key !== " ") return;
      evento.preventDefault();
      accion();
    },
  };
}
