import { useLocation } from "react-router-dom";

import { useCurrentUser } from "../auth/useCurrentUser";
import { resolverVolver, type DestinoVolver, type TipoVolver } from "./volver";

/** Destino de "Volver" de la página actual según su estado de navegación y los permisos. */
export function useDestinoVolver(tipo: TipoVolver): DestinoVolver {
  const { state } = useLocation();
  const { user } = useCurrentUser();
  return resolverVolver(
    tipo,
    (state as { volverA?: unknown } | null)?.volverA,
    user?.permissions ?? [],
  );
}
