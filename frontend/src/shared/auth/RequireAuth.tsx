import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Navigate, Outlet, useLocation } from "react-router-dom";

import { alRecibirNoAutorizado } from "../api/client";
import { obtenerSesionDesarrollo } from "./dev/session";
import { RUTA_SESION, sesionKeys } from "./sesionMicrosoft";
import { useCurrentUser } from "./useCurrentUser";

/** Layout-route gate: renders the protected tree only when there is a session. */
export function RequireAuth() {
  const location = useLocation();
  const cliente = useQueryClient();
  const { user, isLoading, error } = useCurrentUser();

  // Un 401 de la API significa que la cookie venció o el usuario fue desactivado:
  // se descarta la sesión y el guard vuelve al login.
  useEffect(
    () =>
      alRecibirNoAutorizado((url) => {
        if (url !== RUTA_SESION && obtenerSesionDesarrollo() === null) {
          cliente.setQueryData(sesionKeys.actual, null);
        }
      }),
    [cliente],
  );

  if (isLoading) return <main role="status">Cargando sesión…</main>;
  // Con error, AppLayout muestra el aviso y permite reintentar.
  if (!user && !error) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }
  return <Outlet />;
}
