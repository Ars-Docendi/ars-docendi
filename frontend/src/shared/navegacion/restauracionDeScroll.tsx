import { useEffect, useLayoutEffect, useRef } from "react";
import { useLocation, useNavigationType } from "react-router-dom";

import { asegurarAlmacen, persistir, posiciones } from "./posicionesDeScroll";
import { ESTADO_ES_VOLVER } from "./volver";

/**
 * El scroll de la app es `.adoc-main` (no la ventana). Se guarda por entrada del historial
 * (`location.key`, para Atrás/Adelante) y por `pathname + search` (para "Volver", que es una
 * navegación nueva a una lista ya visitada). Una página nueva arranca arriba y un `replace` que
 * sólo cambia el query (un filtro) conserva el scroll.
 */
function principal() {
  return document.querySelector<HTMLElement>(".adoc-main");
}

const INTENTOS_MAXIMOS = 50;
const INTERVALO_MS = 60;

/** Aplica `y` en cuanto el contenido es lo bastante alto (los datos llegan después de la ruta). */
function restaurar(y: number): () => void {
  const main = principal();
  if (!main) return () => undefined;
  if (y <= 0) {
    main.scrollTop = 0;
    return () => undefined;
  }
  let intentos = 0;
  const intentar = () => {
    const actual = principal();
    if (!actual) return true;
    actual.scrollTop = y;
    return actual.scrollTop >= y - 2 || ++intentos > INTENTOS_MAXIMOS;
  };
  if (intentar()) return () => undefined;
  const reloj = window.setInterval(() => {
    if (intentar()) window.clearInterval(reloj);
  }, INTERVALO_MS);
  return () => window.clearInterval(reloj);
}

export function RestauradorDeScroll() {
  const ubicacion = useLocation();
  const tipo = useNavigationType();
  const previa = useRef<{ key: string; path: string; ruta: string } | null>(null);
  const ultimoScroll = useRef(0);

  useEffect(() => {
    if ("scrollRestoration" in window.history) window.history.scrollRestoration = "manual";
  }, []);

  // El scroll vigente de la entrada actual, siempre al día: al cambiar la ruta el contenido ya
  // cambió y `scrollTop` no sirve para leer la posición de la entrada que se dejó.
  useEffect(() => {
    const main = principal();
    if (!main) return;
    const alScroll = () => {
      ultimoScroll.current = main.scrollTop;
    };
    main.addEventListener("scroll", alScroll, { passive: true });
    return () => main.removeEventListener("scroll", alScroll);
  }, []);

  useLayoutEffect(() => {
    asegurarAlmacen();
    const ruta = ubicacion.pathname + ubicacion.search;
    const anterior = previa.current;
    if (anterior) {
      posiciones.set(`k:${anterior.key}`, ultimoScroll.current);
      posiciones.set(`r:${anterior.ruta}`, ultimoScroll.current);
      persistir();
    }
    previa.current = { key: ubicacion.key, path: ubicacion.pathname, ruta };

    const esVolver =
      (ubicacion.state as Record<string, unknown> | null)?.[ESTADO_ES_VOLVER] === true;
    let destino = 0;
    if (tipo === "POP") destino = posiciones.get(`k:${ubicacion.key}`) ?? 0;
    else if (esVolver) destino = posiciones.get(`r:${ruta}`) ?? 0;
    else if (anterior && anterior.path === ubicacion.pathname) {
      // Un filtro (replace con otro query) o un hash: se queda donde está.
      ultimoScroll.current = principal()?.scrollTop ?? ultimoScroll.current;
      return;
    }
    ultimoScroll.current = destino;
    return restaurar(destino);
  }, [ubicacion, tipo]);

  return null;
}
