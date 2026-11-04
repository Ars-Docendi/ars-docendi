import { useSyncExternalStore } from "react";

/** Same breakpoint as the stacked layout in `ayuda.css`: below it the sidebar is not rendered. */
const CONSULTA_ANGOSTA = "(max-width: 900px)";

function suscribir(avisar: () => void) {
  const consulta = window.matchMedia?.(CONSULTA_ANGOSTA);
  consulta?.addEventListener("change", avisar);
  return () => consulta?.removeEventListener("change", avisar);
}

export function usePantallaAngosta(): boolean {
  return useSyncExternalStore(
    suscribir,
    () => window.matchMedia?.(CONSULTA_ANGOSTA).matches ?? false,
    () => false,
  );
}
