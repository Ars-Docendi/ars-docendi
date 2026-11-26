import {
  useLayoutEffect,
  useRef,
  useState,
  type CSSProperties,
  type KeyboardEvent,
  type PointerEvent,
  type ReactNode,
  type RefObject,
} from "react";
import { createPortal } from "react-dom";

/** Column widths in px, by header position: the measured start and the user's current ones. */
export interface AnchosDeColumnas {
  iniciales: number[];
  actuales: number[];
}

const ANCHO_MINIMO = 64;
const PASO_DE_TECLADO = 16;
/** Two taps on the same handle within this time restore the column. */
const DOBLE_TOQUE_MS = 350;
/** Pointer travel below this is a tap, not a drag. */
const UMBRAL_DE_ARRASTRE = 3;
const CLASE_ASA = "adoc-ajustar-columna";

interface Encabezado {
  celda: HTMLTableCellElement;
  nombre: string;
}

interface Arrastre {
  indice: number;
  x: number;
  base: AnchosDeColumnas;
  movido: boolean;
}

interface ColumnasAjustables {
  /** The resize handles, portaled into the header cells. */
  asas: ReactNode;
  /** Classes for the `<table>`: handle layout, plus fixed layout once widths are in effect. */
  clase: string;
  estilo: CSSProperties | undefined;
}

/**
 * Lets the user resize the columns of the table inside `regionRef`: a handle at the right edge of
 * every header cell (drag, ArrowLeft/ArrowRight, double tap or double click to restore). Widths
 * are measured from the rendered columns when the first one changes, so the table looks exactly
 * as before until a handle is used. The header cells are found in the DOM, so callers keep
 * their own `<th>` markup. Inactive (no handles, no widths) when `activo` is false.
 */
export function useColumnasAjustables(
  regionRef: RefObject<HTMLElement | null>,
  activo: boolean,
  anchos: AnchosDeColumnas | null,
  onCambio: (anchos: AnchosDeColumnas) => void,
): ColumnasAjustables {
  const [encabezados, setEncabezados] = useState<Encabezado[]>([]);
  const arrastre = useRef<Arrastre | null>(null);
  const ultimoToque = useRef<{ indice: number; instante: number } | null>(null);
  const restaurarAlClic = useRef<number | null>(null);

  // The header cells belong to the caller, which may add, remove or rename columns at any time.
  useLayoutEffect(() => {
    const el = regionRef.current;
    const leer = () => {
      const siguientes = activo && el ? leerEncabezados(el) : [];
      setEncabezados((actuales) =>
        mismosEncabezados(actuales, siguientes) ? actuales : siguientes,
      );
    };
    leer();
    if (!activo || !el || typeof MutationObserver === "undefined") return;
    const observador = new MutationObserver(leer);
    observador.observe(el, { childList: true, subtree: true, characterData: true });
    return () => observador.disconnect();
  }, [activo, regionRef]);

  // Widths only apply while they match the columns on screen; a different column set starts over.
  const vigentes =
    anchos && encabezados.length > 0 && anchos.actuales.length === encabezados.length
      ? anchos
      : null;

  // The first row's cells set the columns under `table-layout: fixed`. They belong to the
  // caller's markup, so the width is written on the element and cleared when it no longer applies.
  useLayoutEffect(() => {
    encabezados.forEach(({ celda }, i) => {
      celda.style.width = vigentes ? `${vigentes.actuales[i]}px` : "";
    });
    return () => encabezados.forEach(({ celda }) => (celda.style.width = ""));
  }, [encabezados, vigentes]);

  function base(): AnchosDeColumnas {
    if (vigentes) return vigentes;
    // Rounded up: a column a fraction of a pixel narrower than its content would wrap it.
    const medidos = encabezados.map(({ celda }) => Math.ceil(celda.getBoundingClientRect().width));
    return { iniciales: medidos, actuales: medidos };
  }

  function fijar(desde: AnchosDeColumnas, indice: number, ancho: number) {
    const actuales = desde.actuales.slice();
    actuales[indice] = ancho;
    onCambio({ iniciales: desde.iniciales, actuales });
  }

  function restablecer(indice: number) {
    if (vigentes) fijar(vigentes, indice, vigentes.iniciales[indice]);
  }

  function empezar(indice: number, evento: PointerEvent<HTMLButtonElement>) {
    // Keeps the gesture away from the header's own controls (sort) and from page scrolling.
    evento.preventDefault();
    evento.stopPropagation();
    evento.currentTarget.setPointerCapture?.(evento.pointerId);
    arrastre.current = { indice, x: evento.clientX, base: base(), movido: false };
  }

  function mover(indice: number, evento: PointerEvent<HTMLButtonElement>) {
    const actual = arrastre.current;
    if (!actual || actual.indice !== indice) return;
    const desplazamiento = evento.clientX - actual.x;
    if (Math.abs(desplazamiento) >= UMBRAL_DE_ARRASTRE) actual.movido = true;
    if (!actual.movido) return;
    const ancho = actual.base.actuales[indice] + desplazamiento;
    fijar(actual.base, indice, Math.max(ANCHO_MINIMO, Math.round(ancho)));
  }

  function soltar(indice: number, evento: PointerEvent<HTMLButtonElement>) {
    const actual = arrastre.current;
    arrastre.current = null;
    // Touch browsers do not reliably fire `dblclick` on a double tap; mice get it natively.
    if (!actual || actual.movido || evento.pointerType === "mouse") return;
    const previo = ultimoToque.current;
    if (previo && previo.indice === indice && evento.timeStamp - previo.instante < DOBLE_TOQUE_MS) {
      ultimoToque.current = null;
      // Restored on the tap's click, not here: the click is hit-tested after `pointerup`, and a
      // column that already shrank would hand it to the control now under the finger (sort).
      restaurarAlClic.current = indice;
    } else {
      ultimoToque.current = { indice, instante: evento.timeStamp };
    }
  }

  function teclear(indice: number, evento: KeyboardEvent<HTMLButtonElement>) {
    const paso =
      evento.key === "ArrowRight"
        ? PASO_DE_TECLADO
        : evento.key === "ArrowLeft"
          ? -PASO_DE_TECLADO
          : 0;
    if (!paso) return;
    evento.preventDefault();
    const desde = base();
    fijar(desde, indice, Math.max(ANCHO_MINIMO, desde.actuales[indice] + paso));
  }

  const asas = encabezados.map(({ celda, nombre }, indice) =>
    createPortal(
      <button
        type="button"
        className={CLASE_ASA}
        aria-label={`Ajustar ancho de «${nombre}»`}
        title="Arrastrá para cambiar el ancho; doble toque lo restablece"
        onPointerDown={(e) => empezar(indice, e)}
        onPointerMove={(e) => mover(indice, e)}
        onPointerUp={(e) => soltar(indice, e)}
        onPointerCancel={() => (arrastre.current = null)}
        onKeyDown={(e) => teclear(indice, e)}
        onDoubleClick={() => restablecer(indice)}
        onClick={(e) => {
          e.stopPropagation();
          if (restaurarAlClic.current !== indice) return;
          restaurarAlClic.current = null;
          restablecer(indice);
        }}
      />,
      celda,
      indice,
    ),
  );

  const clase = [
    encabezados.length > 0 && "adoc-tabla-ajustable",
    vigentes && "adoc-tabla-ajustable--con-anchos",
  ]
    .filter(Boolean)
    .join(" ");
  const estilo = vigentes ? { width: vigentes.actuales.reduce((a, b) => a + b, 0) } : undefined;

  return { asas, clase, estilo };
}

function leerEncabezados(region: HTMLElement): Encabezado[] {
  const celdas = region.querySelectorAll<HTMLTableCellElement>(
    ":scope > table > thead > tr:first-child > th",
  );
  return Array.from(celdas, (celda) => ({ celda, nombre: nombreDeColumna(celda) }));
}

/** The header's visible text: screen-reader-only hints, icons and the handle itself are left out. */
function nombreDeColumna(celda: HTMLElement): string {
  let texto = "";
  const recorrer = (nodo: Node) => {
    if (nodo.nodeType === Node.TEXT_NODE) {
      texto += nodo.textContent ?? "";
      return;
    }
    if (
      nodo instanceof Element &&
      (nodo.classList.contains("adoc-sr") ||
        nodo.classList.contains(CLASE_ASA) ||
        nodo.getAttribute("aria-hidden") === "true")
    ) {
      return;
    }
    nodo.childNodes.forEach(recorrer);
  };
  celda.childNodes.forEach(recorrer);
  return texto.replace(/\s+/g, " ").trim();
}

function mismosEncabezados(a: Encabezado[], b: Encabezado[]): boolean {
  return (
    a.length === b.length && a.every((e, i) => e.celda === b[i].celda && e.nombre === b[i].nombre)
  );
}
