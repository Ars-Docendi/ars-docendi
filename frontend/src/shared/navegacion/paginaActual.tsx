import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { useLocation } from "react-router-dom";

import "./navegacion.css";
import { ID_CONTENIDO, SUFIJO_TITULO, tituloDocumento } from "./pagina";

/** "Saltar al contenido": primer elemento enfocable, visible sólo con el foco. */
export function EnlaceSaltar() {
  return (
    <a
      href={`#${ID_CONTENIDO}`}
      className="adoc-saltar"
      onClick={(evento) => {
        evento.preventDefault();
        document.getElementById(ID_CONTENIDO)?.focus();
      }}
    >
      Saltar al contenido
    </a>
  );
}

const REINTENTOS = 40;
const ESPERA_MS = 50;
const VIGILANCIA_MS = 5000;

/**
 * En cada cambio de página (no de query): título base mientras carga, foco al `h1` y su texto en
 * una región `aria-live` educada. La página puede tardar en renderizar el `h1` (datos, rutas
 * perezosas), por eso se reintenta unos instantes.
 */
export function SeguimientoDePagina() {
  const { pathname } = useLocation();
  const previo = useRef<string | null>(null);
  const [anuncio, setAnuncio] = useState("");

  // Antes que los efectos de la página: si ella no fija título, queda el del producto.
  useLayoutEffect(() => {
    if (previo.current !== null && previo.current !== pathname) document.title = SUFIJO_TITULO;
  }, [pathname]);

  useEffect(() => {
    const primera = previo.current === null;
    const cambio = previo.current !== pathname;
    previo.current = pathname;
    // La carga inicial no roba el foco: sólo la navegación dentro de la app.
    if (primera || !cambio) return;
    let intentos = 0;
    let enfocado: HTMLElement | null = null;
    let observador: MutationObserver | null = null;
    let vigilancia: number | undefined;
    const enfocar = (titulo: HTMLElement) => {
      titulo.focus({ preventScroll: true });
      enfocado = titulo;
      const texto = titulo.textContent?.trim() ?? "";
      // Páginas que no usan PageHeader (p. ej. la ayuda) igual titulan el documento.
      if (texto && document.title === SUFIJO_TITULO) document.title = tituloDocumento(texto);
      setAnuncio(texto);
    };
    // Una página que muestra un h1 provisorio y lo reemplaza al cargar deja el foco en el body:
    // si el h1 que enfocamos se desmontó y nadie movió el foco, pasa al h1 nuevo.
    const vigilar = () => {
      observador = new MutationObserver(() => {
        if (!enfocado || enfocado.isConnected) return;
        const activo = document.activeElement;
        if (activo && activo !== document.body) return;
        const nuevo = document.querySelector<HTMLElement>(".adoc-main h1");
        if (nuevo) enfocar(nuevo);
      });
      observador.observe(document.body, { childList: true, subtree: true });
      vigilancia = window.setTimeout(() => observador?.disconnect(), VIGILANCIA_MS);
    };
    const buscar = () => {
      const titulo = document.querySelector<HTMLElement>(".adoc-main h1");
      if (titulo) {
        enfocar(titulo);
        vigilar();
        return true;
      }
      return ++intentos > REINTENTOS;
    };
    const limpiar = () => {
      observador?.disconnect();
      window.clearTimeout(vigilancia);
    };
    if (buscar()) return limpiar;
    const reloj = window.setInterval(() => {
      if (buscar()) window.clearInterval(reloj);
    }, ESPERA_MS);
    return () => {
      window.clearInterval(reloj);
      limpiar();
    };
  }, [pathname]);

  return (
    <div className="adoc-solo-lectores" role="status" aria-live="polite" aria-atomic="true">
      {anuncio}
    </div>
  );
}
