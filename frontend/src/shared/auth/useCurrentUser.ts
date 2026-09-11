import { useSyncExternalStore } from "react";
import { developmentAuthEnabled } from "./developmentAuth";
import { obtenerSesionDesarrollo, suscribirSesionDesarrollo } from "./dev/session";
import { useIdentidadesDesarrollo } from "./dev/useIdentidadesDesarrollo";

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

function useCurrentUserDesarrollo(): CurrentUserState {
  const sesion = useSyncExternalStore(
    suscribirSesionDesarrollo,
    obtenerSesionDesarrollo,
    () => null,
  );
  const consulta = useIdentidadesDesarrollo();
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

function useCurrentUserProduccion(): CurrentUserState {
  return {
    user: null,
    isLoading: false,
    error: new Error("La integración de identidad institucional todavía no está configurada."),
    retry: () => undefined,
  };
}

export const useCurrentUser = developmentAuthEnabled
  ? useCurrentUserDesarrollo
  : useCurrentUserProduccion;

function iniciales(nombre: string): string {
  return nombre
    .split(/\s+/)
    .slice(0, 2)
    .map((parte) => parte[0])
    .join("")
    .toUpperCase();
}
