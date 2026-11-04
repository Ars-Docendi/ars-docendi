import { useEffect } from "react";
import { useLocation } from "react-router-dom";

/** Banda superior del área visible: el paso que la cruza es el que se está leyendo. */
const MARGEN_BANDA_ACTIVA = "0px 0px -70% 0px";
/** Tras un clic o un deep link el scroll-spy se calla mientras dura el desplazamiento. */
const PAUSA_INICIAL_MS = 600;
const REPOSO_DEL_SCROLL_MS = 150;

function buscarContenedorScrolleable(elemento: HTMLElement): HTMLElement | null {
  let nodo = elemento.parentElement;
  while (nodo) {
    const { overflowY } = getComputedStyle(nodo);
    if (overflowY === "auto" || overflowY === "scroll") return nodo;
    nodo = nodo.parentElement;
  }
  return null;
}

/**
 * Scroll-spy de los pasos de un artículo: informa con `alCambiar` cuál es el paso
 * visible (por `IntersectionObserver`). Los pasos deben estar en el DOM al montar.
 * Un hash `#paso-N` en la URL lo fija de entrada; `alCambiar` debe ser estable.
 * Los pasos cortos del final nunca cruzan la banda superior: al llegar al fondo se
 * activa el último.
 */
export function usePasoActivo(
  idsPasos: readonly string[],
  alCambiar: (id: string | undefined) => void,
) {
  const { key, hash } = useLocation();
  const clave = idsPasos.join("|");

  useEffect(() => {
    const ids = clave === "" ? [] : clave.split("|");
    const destino = ids.find((id) => id === hash.slice(1));
    alCambiar(destino ?? ids[0]);

    let pausado = false;
    let temporizador: ReturnType<typeof setTimeout> | undefined;
    const pausar = (ms: number) => {
      pausado = true;
      clearTimeout(temporizador);
      temporizador = setTimeout(() => {
        pausado = false;
      }, ms);
    };
    if (destino) pausar(PAUSA_INICIAL_MS);

    const elementos = ids
      .map((id) => document.getElementById(id))
      .filter((elemento): elemento is HTMLElement => elemento !== null);

    let observador: IntersectionObserver | undefined;
    let contenedor: HTMLElement | Window = window;
    const alScrollear = () => {
      if (pausado) return pausar(REPOSO_DEL_SCROLL_MS);
      const base = contenedor instanceof Window ? document.documentElement : contenedor;
      if (base.scrollTop + base.clientHeight >= base.scrollHeight - 2) {
        alCambiar(elementos[elementos.length - 1].id);
      }
    };

    if (elementos.length > 0) {
      observador = new IntersectionObserver(
        (entradas) => {
          const visible = entradas.find((entrada) => entrada.isIntersecting);
          if (visible && !pausado) alCambiar(visible.target.id);
        },
        { rootMargin: MARGEN_BANDA_ACTIVA },
      );
      elementos.forEach((elemento) => observador?.observe(elemento));
      contenedor = buscarContenedorScrolleable(elementos[0]) ?? window;
      contenedor.addEventListener("scroll", alScrollear, { passive: true });
    }

    return () => {
      observador?.disconnect();
      clearTimeout(temporizador);
      contenedor.removeEventListener("scroll", alScrollear);
      alCambiar(undefined);
    };
    // `key` re-ejecuta el efecto ante cada navegación, también al repetir el mismo hash.
  }, [clave, hash, key, alCambiar]);
}
