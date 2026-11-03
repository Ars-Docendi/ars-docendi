import { useSyncExternalStore } from "react";

/** Same breakpoint as `shell.css`: below it the fixed sidebar is not shown. */
const CONSULTA_ANGOSTA = "(max-width: 767px)";

function suscribirAncho(avisar: () => void) {
  const consulta = window.matchMedia?.(CONSULTA_ANGOSTA);
  consulta?.addEventListener("change", avisar);
  return () => consulta?.removeEventListener("change", avisar);
}

export function usePantallaAngosta(): boolean {
  return useSyncExternalStore(
    suscribirAncho,
    () => window.matchMedia?.(CONSULTA_ANGOSTA).matches ?? false,
    () => false,
  );
}
