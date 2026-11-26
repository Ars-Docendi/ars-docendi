import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type CSSProperties,
  type ReactNode,
} from "react";

import "./ContenedorTabla.css";
import { useColumnasAjustables, type AnchosDeColumnas } from "./ColumnasAjustables";
import { usePantallaTelefono } from "./usePantallaTelefono";

interface ContenedorTablaProps {
  /** Accessible name of the scroll region ("Personas"). */
  etiqueta: string;
  /** Height limit of the scroll area; ignored on phones, where the page scrolls. */
  maxHeight?: string;
  className?: string;
  /**
   * Column widths the user set on a phone. Passed together with `onAnchosChange`, the caller owns
   * them, so two tables showing the same data (inline and expanded result) share them; without
   * it, each table keeps its own for as long as it is mounted.
   */
  anchos?: AnchosDeColumnas | null;
  onAnchosChange?: (anchos: AnchosDeColumnas) => void;
  children: ReactNode;
}

/**
 * Scroll container of a data table: a focusable region (keyboard scroll), sticky first column,
 * a shadow on the right edge while more columns remain, and `scroll-padding` so a focused
 * control never ends up behind the sticky header or column. On phones every header cell gets a
 * handle to change its column's width (see `useColumnasAjustables`). Replaces `Table.Root`; it
 * must be rendered inside `<Table>`.
 */
export function ContenedorTabla({
  etiqueta,
  maxHeight,
  className,
  anchos,
  onAnchosChange,
  children,
}: ContenedorTablaProps) {
  const marco = useRef<HTMLDivElement>(null);
  const region = useRef<HTMLDivElement>(null);
  const telefono = usePantallaTelefono();
  const [anchosPropios, setAnchosPropios] = useState<AnchosDeColumnas | null>(null);
  const columnas = useColumnasAjustables(
    region,
    telefono,
    onAnchosChange ? (anchos ?? null) : anchosPropios,
    onAnchosChange ?? setAnchosPropios,
  );

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
        <table
          className={`adoc-table${columnas.clase ? ` ${columnas.clase}` : ""}`}
          style={columnas.estilo}
        >
          {children}
        </table>
        {columnas.asas}
      </div>
    </div>
  );
}
