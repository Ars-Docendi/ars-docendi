import { useSyncExternalStore } from "react";

/** Same breakpoint as the phone paddings in `shell.css`: below it lists render as cards. */
const CONSULTA_TELEFONO = "(max-width: 559px)";

function suscribir(avisar: () => void) {
  const consulta = window.matchMedia?.(CONSULTA_TELEFONO);
  consulta?.addEventListener("change", avisar);
  return () => consulta?.removeEventListener("change", avisar);
}

export function usePantallaTelefono(): boolean {
  return useSyncExternalStore(
    suscribir,
    () => window.matchMedia?.(CONSULTA_TELEFONO).matches ?? false,
    () => false,
  );
}
