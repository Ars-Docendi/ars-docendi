import { useSyncExternalStore } from "react";
import { useQuery } from "@tanstack/react-query";
import { developmentAuthEnabled } from "./developmentAuth";
import { obtenerSesionDesarrolloApi } from "./dev/identidadesApi";
import { obtenerSesionDesarrollo, suscribirSesionDesarrollo } from "./dev/session";
import {
  microsoftLoginEnabled,
  obtenerSesion,
  sesionKeys,
  type AsignacionApi,
  type SesionApi,
} from "./sesionMicrosoft";

export type Role = string;

export interface CurrentUser {
  id: string;
  name: string;
  initials: string;
  upn: string;
  role: Role;
  roleCode: string;
  permissions: string[];
  /** Asignación con la que opera; `null` mientras falta elegirla. */
  asignacion: AsignacionApi | null;
  asignaciones: AsignacionApi[];
  requiereElegirRol: boolean;
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
  const consulta = useQuery({
    queryKey: [...sesionKeys.desarrollo, sesion?.usuarioId, sesion?.asignacionId],
    queryFn: obtenerSesionDesarrolloApi,
    enabled: habilitado && sesion !== null,
    retry: false,
  });
  return {
    user: sesion && consulta.data ? aUsuario(consulta.data) : null,
    isLoading: consulta.isLoading,
    error: consulta.error ? new Error("La sesión elegida ya no está disponible.") : null,
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
  return {
    user: consulta.data ? aUsuario(consulta.data) : null,
    isLoading: consulta.isLoading,
    error: consulta.error,
    retry: () => {
      void consulta.refetch();
    },
  };
}

function aUsuario(sesion: SesionApi): CurrentUser {
  return {
    id: sesion.usuarioId,
    name: sesion.nombreParaMostrar,
    initials: iniciales(sesion.nombreParaMostrar),
    upn: sesion.upn,
    role: sesion.rolNombre ?? "",
    roleCode: sesion.rolCodigo ?? "",
    permissions: sesion.permisos,
    asignacion: sesion.asignacion,
    asignaciones: sesion.asignaciones,
    requiereElegirRol: sesion.requiereElegirRol,
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
