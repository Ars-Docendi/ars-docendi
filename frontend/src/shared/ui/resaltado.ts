import { useEffect, useSyncExternalStore } from "react";

import "./resaltado.css";

/**
 * Brief highlight of the entity a notification just navigated to. A tiny store (one id) so the
 * panel can set it before the destination page exists; the page reads it with `useResaltado`.
 */
const DURACION_MS = 3000;

let actual: string | null = null;
let temporizador: ReturnType<typeof setTimeout> | undefined;
const oyentes = new Set<() => void>();

function emitir() {
  oyentes.forEach((oyente) => oyente());
}

export function resaltar(entidadId: string) {
  actual = entidadId;
  emitir();
  clearTimeout(temporizador);
  temporizador = setTimeout(quitarResaltado, DURACION_MS);
}

export function quitarResaltado() {
  clearTimeout(temporizador);
  actual = null;
  emitir();
}

/**
 * True while `entidadId` is the highlighted entity. The element carrying `data-entidad-id` is
 * scrolled into view once it exists (the page may still be loading when the highlight starts).
 */
export function useResaltado(entidadId: string | undefined): boolean {
  const id = useSyncExternalStore(
    (oyente) => {
      oyentes.add(oyente);
      return () => oyentes.delete(oyente);
    },
    () => actual,
  );
  const resaltada = entidadId !== undefined && id === entidadId;

  useEffect(() => {
    if (!resaltada) return;
    let intentos = 0;
    const reloj = setInterval(() => {
      const el = document.querySelector(`[data-entidad-id="${entidadId}"]`);
      if (el) {
        clearInterval(reloj);
        el.scrollIntoView?.({ block: "center" });
      } else if (++intentos > 30) clearInterval(reloj);
    }, 120);
    return () => clearInterval(reloj);
  }, [resaltada, entidadId]);

  return resaltada;
}
