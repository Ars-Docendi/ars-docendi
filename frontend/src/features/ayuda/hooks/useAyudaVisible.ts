import { useMemo } from "react";

import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { articulosVisibles, modulosVisibles } from "../utils/visibilidad";

const SIN_PERMISOS: readonly string[] = [];

/**
 * Contenido de ayuda visible para la sesión vigente (por permisos, nunca por
 * nombre de rol). Toda página y componente de ayuda parte de acá.
 */
export function useAyudaVisible() {
  const permisos = useCurrentUser().user?.permissions ?? SIN_PERMISOS;
  return useMemo(
    () => ({
      permisos,
      articulos: articulosVisibles(permisos),
      modulos: modulosVisibles(permisos),
    }),
    [permisos],
  );
}
