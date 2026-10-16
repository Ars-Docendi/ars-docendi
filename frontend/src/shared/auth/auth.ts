import { useQueryClient } from "@tanstack/react-query";
import { limpiarSesionDesarrollo } from "./dev/session";
import { developmentAuthEnabled } from "./developmentAuth";
import { cerrarSesion, microsoftLoginEnabled, sesionKeys } from "./sesionMicrosoft";

/**
 * Cierra la sesión de Ars Docendi: la selección de desarrollo y la cookie del
 * ingreso con Microsoft. La sesión de la persona en Microsoft no se toca.
 */
export function useCerrarSesion(): () => Promise<void> {
  const cliente = useQueryClient();
  return async () => {
    if (developmentAuthEnabled) limpiarSesionDesarrollo();
    if (microsoftLoginEnabled) await cerrarSesion();
    cliente.setQueryData(sesionKeys.actual, null);
  };
}
