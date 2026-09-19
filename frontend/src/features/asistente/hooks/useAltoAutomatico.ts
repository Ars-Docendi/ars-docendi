import { useLayoutEffect, type RefObject } from "react";

interface OpcionesDelAlto {
  /** Hasta cuántas líneas crece antes de scrollear por dentro. */
  maxLineas?: number;
}

/**
 * El campo crece con lo que se escribe, hasta un tope de líneas.
 *
 * ES UN FALLBACK. En los navegadores que entienden `field-sizing: content` no hace
 * nada: la hoja de estilos ya lo resuelve, con `max-height` como tope. Firefox y
 * Safari no lo entienden y ahí el campo se quedaba en una línea con scroll
 * adentro, que es lo que hace que una pregunta larga se lea por el ojo de una
 * cerradura. Acá se mide el contenido y se fija el alto a mano, con el mismo tope.
 *
 * `useLayoutEffect` y no `useEffect` para medir y fijar antes de pintar: con
 * `useEffect` el campo se ve un instante con el alto viejo y después salta.
 */
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

    // La caja es `border-box` (lo fija la librería bajo `.adoc-ui`), así que el
    // alto incluye relleno y borde. `scrollHeight` ya trae el relleno; el borde
    // hay que sumarlo. Si el alto de línea no se puede medir no hay tope: mejor un
    // campo que crece de más que uno que se corta.
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
