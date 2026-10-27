import { useLayoutEffect, type RefObject } from "react";

interface OpcionesDelAlto {
  /** Hasta cuántas líneas crece antes de scrollear por dentro. */
  maxLineas?: number;
}

/** El campo crece con lo que se escribe, hasta un tope de líneas. */
export function useAltoAutomatico(
  ref: RefObject<HTMLTextAreaElement | null>,
  valor: string,
  { maxLineas = 6 }: OpcionesDelAlto = {},
) {
  useLayoutEffect(() => {
    const campo = ref.current;
    if (!campo || soportaFieldSizing()) return;

    // A `auto` primero para que `scrollHeight` mida el contenido y no el alto
    // que se le fijó la vez anterior: sin esto el campo crece pero nunca achica.
    campo.style.height = "auto";

    const estilos = getComputedStyle(campo);
    const lineaPx = parseFloat(estilos.lineHeight);
    const rellenoPx = parseFloat(estilos.paddingTop) + parseFloat(estilos.paddingBottom);
    const bordePx = parseFloat(estilos.borderTopWidth) + parseFloat(estilos.borderBottomWidth);

    // La caja es `border-box` (lo fija la librería bajo `.adoc-ui`), así que el alto incluye
    // relleno y borde.
    const tope = Number.isFinite(lineaPx) ? lineaPx * maxLineas + rellenoPx + bordePx : Infinity;
    const alto = Math.min(campo.scrollHeight + bordePx, tope);

    // Sin layout —jsdom— `scrollHeight` es 0 y fijarlo dejaría el campo invisible.
    if (alto > 0) campo.style.height = `${alto}px`;
  }, [ref, valor, maxLineas]);
}

function soportaFieldSizing(): boolean {
  return (
    typeof CSS !== "undefined" &&
    typeof CSS.supports === "function" &&
    CSS.supports("field-sizing", "content")
  );
}
