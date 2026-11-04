import { useCallback } from "react";
import { useLocation, useNavigate } from "react-router-dom";

import { estadoVolver } from "./volver";

/** Estado de navegación (`volverA`) para abrir un detalle desde la ubicación actual. */
export function useEstadoVolver() {
  const ubicacion = useLocation();
  return estadoVolver(ubicacion);
}

/** `abrir(ruta)`: navega a un detalle recordando de qué lista (con sus filtros) se vino. */
export function useAbrirDetalle() {
  const navegar = useNavigate();
  const ubicacion = useLocation();
  return useCallback(
    (ruta: string) => navegar(ruta, { state: estadoVolver(ubicacion) }),
    [navegar, ubicacion],
  );
}
