import { useCallback, useEffect, useRef, type CSSProperties, type ReactNode } from "react";

import "./ContenedorTabla.css";
import { usePantallaTelefono } from "./usePantallaTelefono";

interface ContenedorTablaProps {
  /** Accessible name of the scroll region ("Personas"). */
  etiqueta: string;
  /** Height limit of the scroll area; ignored on phones, where the page scrolls. */
  maxHeight?: string;
  className?: string;
  children: ReactNode;
}

/**
 * Scroll container of a data table: a focusable region (keyboard scroll), sticky first column,
 * a shadow on the right edge while more columns remain, and `scroll-padding` so a focused
 * control never ends up behind the sticky header or column. Replaces `Table.Root`; it must be
 * rendered inside `<Table>`.
 */
export function ContenedorTabla({
  etiqueta,
  maxHeight,
  className,
  children,
}: ContenedorTablaProps) {
  const marco = useRef<HTMLDivElement>(null);
  const region = useRef<HTMLDivElement>(null);
  const telefono = usePantallaTelefono();

  const medir = useCallback(() => {
    const el = region.current;
    if (!el) return;
    const restante = el.scrollWidth - el.clientWidth - el.scrollLeft;
    marco.current?.toggleAttribute("data-desborde", restante > 1);
    marco.current?.toggleAttribute("data-desplazada", el.scrollLeft > 1);
    const primera = el.querySelector<HTMLElement>("thead th:first-child");
    if (primera) {
      el.style.setProperty("--ancho-col-fija", `${primera.offsetWidth}px`);
      el.style.setProperty("--alto-encabezado", `${primera.offsetHeight}px`);
    }
  }, []);

  useEffect(() => {
    medir();
    const el = region.current;
    if (!el || typeof ResizeObserver === "undefined") return;
    const observador = new ResizeObserver(medir);
    observador.observe(el);
    const tabla = el.querySelector("table");
    if (tabla) observador.observe(tabla);
    return () => observador.disconnect();
  }, [medir]);

  const estilo: CSSProperties | undefined = maxHeight && !telefono ? { maxHeight } : undefined;

  return (
    <div ref={marco} className="adoc-contenedor-tabla-marco">
      <div
        ref={region}
        role="region"
        tabIndex={0}
        aria-label={etiqueta}
        className={`adoc-table-scroll adoc-contenedor-tabla${className ? ` ${className}` : ""}`}
        style={estilo}
        onScroll={medir}
      >
        <table className="adoc-table">{children}</table>
      </div>
    </div>
  );
}
