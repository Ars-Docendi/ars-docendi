import { useSyncExternalStore } from "react";
import { useQuery } from "@tanstack/react-query";
import { developmentAuthEnabled } from "./developmentAuth";
import { obtenerSesionDesarrollo, suscribirSesionDesarrollo } from "./dev/session";
import { useIdentidadesDesarrollo } from "./dev/useIdentidadesDesarrollo";
import { microsoftLoginEnabled, obtenerSesion, sesionKeys } from "./sesionMicrosoft";

export type Role = string;

export interface CurrentUser {
  name: string;
  initials: string;
  upn: string;
  role: Role;
  roleCode: string;
  permissions: string[];
}

export interface CurrentUserState {
  user: CurrentUser | null;
  isLoading: boolean;
  error: Error | null;
  retry: () => void;
}

/**
 * Sesión vigente. Una selección del selector de desarrollo manda sobre la cookie;
 * sin selección, la sesión es la cookie del ingreso con Microsoft.
 */
export function useCurrentUser(): CurrentUserState {
  const sesionDesarrollo = useSyncExternalStore(
    suscribirSesionDesarrollo,
    obtenerSesionDesarrollo,
    () => null,
  );
  const usarDesarrollo =
    developmentAuthEnabled && (sesionDesarrollo !== null || !microsoftLoginEnabled);
  const desarrollo = useCurrentUserDesarrollo(usarDesarrollo);
  const microsoft = useCurrentUserMicrosoft(microsoftLoginEnabled && !usarDesarrollo);

  if (usarDesarrollo) return desarrollo;
  if (microsoftLoginEnabled) return microsoft;
  return {
    user: null,
    isLoading: false,
    error: new Error("El ingreso con Microsoft no está habilitado en este ambiente."),
    retry: () => undefined,
  };
}

function useCurrentUserDesarrollo(habilitado: boolean): CurrentUserState {
  const sesion = useSyncExternalStore(
    suscribirSesionDesarrollo,
    obtenerSesionDesarrollo,
    () => null,
  );
  const consulta = useIdentidadesDesarrollo(habilitado && sesion !== null);
  const identidad = consulta.data?.find((item) => item.usuarioId === sesion?.usuarioId);
  const rol = identidad?.roles.find((item) => item.codigo === sesion?.rolCodigo);
  const user =
    identidad && rol
      ? {
          name: identidad.nombreParaMostrar,
          initials: iniciales(identidad.nombreParaMostrar),
          upn: identidad.upn,
          role: rol.nombre,
          roleCode: rol.codigo,
          permissions: rol.permisos,
        }
      : null;
  const seleccionInvalida = Boolean(consulta.data && sesion && !user);
  return {
    user,
    isLoading: consulta.isLoading,
    error:
      consulta.error ??
      (seleccionInvalida ? new Error("La sesión elegida ya no está disponible.") : null),
    retry: () => {
      void consulta.refetch();
    },
  };
}

function useCurrentUserMicrosoft(habilitado: boolean): CurrentUserState {
  const consulta = useQuery({
    queryKey: sesionKeys.actual,
    queryFn: obtenerSesion,
    enabled: habilitado,
    staleTime: 60_000,
  });
  const sesion = consulta.data;
  return {
    user: sesion
      ? {
          name: sesion.nombreParaMostrar,
          initials: iniciales(sesion.nombreParaMostrar),
          upn: sesion.upn,
          role: sesion.rolNombre,
          roleCode: sesion.rolCodigo,
          permissions: sesion.permisos,
        }
      : null,
    isLoading: consulta.isLoading,
    error: consulta.error,
    retry: () => {
      void consulta.refetch();
    },
  };
}

function iniciales(nombre: string): string {
  return nombre
    .split(/\s+/)
    .slice(0, 2)
    .map((parte) => parte[0])
    .join("")
    .toUpperCase();
}
