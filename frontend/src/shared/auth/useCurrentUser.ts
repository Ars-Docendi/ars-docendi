import { useSyncExternalStore } from "react";
import { developmentAuthEnabled } from "./developmentAuth";
import { obtenerSesionDesarrollo, suscribirSesionDesarrollo } from "./dev/session";
import { useIdentidadesDesarrollo } from "./dev/useIdentidadesDesarrollo";

export type Role =
  | "Jefe de Cátedra"
  | "Coordinador"
  | "Secretaría"
  | "Decanato"
  | "Administración"
  | "Docente";

export interface CurrentUser {
  name: string;
  initials: string;
  upn: string;
  role: Role;
  roleCode: string;
}

export interface CurrentUserState {
  user: CurrentUser | null;
  isLoading: boolean;
  error: Error | null;
  retry: () => void;
}

const NOMBRES_ROL: Record<string, Role | undefined> = {
  jefe_catedra: "Jefe de Cátedra",
  coordinador_carrera: "Coordinador",
  secretaria: "Secretaría",
  decanato: "Decanato",
  administrativo: "Administración",
  sys_admin: "Administración",
  docente: "Docente",
};

function useCurrentUserDesarrollo(): CurrentUserState {
  const sesion = useSyncExternalStore(
    suscribirSesionDesarrollo,
    obtenerSesionDesarrollo,
    () => null,
  );
  const consulta = useIdentidadesDesarrollo();
  const identidad = consulta.data?.find((item) => item.usuarioId === sesion?.usuarioId);
  const rol = identidad?.roles.find((item) => item.codigo === sesion?.rolCodigo);
  const nombreRol = rol ? NOMBRES_ROL[rol.codigo] : undefined;
  const user =
    identidad && rol && nombreRol
      ? {
          name: identidad.nombreParaMostrar,
          initials: iniciales(identidad.nombreParaMostrar),
          upn: identidad.upn,
          role: nombreRol,
          roleCode: rol.codigo,
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
