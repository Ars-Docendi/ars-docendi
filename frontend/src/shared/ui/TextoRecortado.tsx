import { useEffect, useRef, useState } from "react";
import type { MouseEvent } from "react";
import { createPortal } from "react-dom";
import "./TextoRecortado.css";

/** Demora breve: evita que el tooltip parpadee al cruzar filas con el mouse. */
const DEMORA_TOOLTIP_MS = 100;

interface TextoRecortadoProps {
  texto: string;
  /**
   * Ancho en px que la columna reserva como mínimo. El texto usa todo el ancho
   * que tenga la columna y solo se recorta con "…" si no le alcanza.
   */
  anchoMinimo: number;
}

interface Posicion {
  top: number;
  left: number;
}

/**
 * Texto de celda con ancho máximo. Si no entra se recorta con "…", y al pasar el
 * mouse se ve completo en un tooltip. El texto completo sigue en la celda, así
 * que lo leen los lectores de pantalla aunque no pasen el mouse.
 */
export function TextoRecortado({ texto, anchoMinimo }: TextoRecortadoProps) {
  const [posicion, setPosicion] = useState<Posicion | null>(null);
  const temporizador = useRef<number | undefined>(undefined);

  function ocultar() {
    window.clearTimeout(temporizador.current);
    setPosicion(null);
  }

  function alPasarElMouse(evento: MouseEvent<HTMLSpanElement>) {
    const elemento = evento.currentTarget;
    // Solo cuando realmente está recortado: si entra, el tooltip sería redundante.
    if (elemento.scrollWidth <= elemento.clientWidth) return;
    const rect = elemento.getBoundingClientRect();
    // La raíz tiene `zoom` (index.css): las medidas vienen escaladas y el tooltip
    // vive dentro de la misma raíz, así que se llevan a su escala.
    const escala = Number.parseFloat(getComputedStyle(document.documentElement).zoom) || 1;
    temporizador.current = window.setTimeout(
      () => setPosicion({ top: rect.bottom / escala + 6, left: rect.left / escala }),
      DEMORA_TOOLTIP_MS,
    );
  }

  // Si la tabla scrollea con el tooltip abierto, quedaría desubicado: se cierra.
  useEffect(() => {
    if (!posicion) return;
    window.addEventListener("scroll", ocultar, true);
    return () => window.removeEventListener("scroll", ocultar, true);
  }, [posicion]);

  useEffect(() => () => window.clearTimeout(temporizador.current), []);

  return (
    <>
      {/* `data-texto` alimenta un medidor invisible (::after) que le pide a la tabla el
          ancho del texto completo: el espacio que sobre va primero a esta columna. */}
      <span
        className="adoc-texto-recortado-caja"
        style={{ minWidth: anchoMinimo }}
        data-texto={texto}
      >
        <span className="adoc-texto-recortado" onMouseEnter={alPasarElMouse} onMouseLeave={ocultar}>
          {texto}
        </span>
      </span>
      {posicion &&
        createPortal(
          <span
            role="tooltip"
            className="adoc-tooltip"
            style={{ top: posicion.top, left: posicion.left }}
          >
            {texto}
          </span>,
          document.body,
        )}
    </>
  );
}
