import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  crearRol,
  editarRol,
  cambiarEstadoRol,
  listarPermisos,
  listarRoles,
  reemplazarPermisos,
} from "../api/rolesApi";
import { identidadesDesarrolloKeys } from "../../../shared/auth/dev/useIdentidadesDesarrollo";
import { sesionKeys } from "../../../shared/auth/sesionMicrosoft";
import type { DatosRolEditables, RolMock } from "../models";

export const rolesKeys = {
  all: ["administracion", "roles"] as const,
  permisos: ["administracion", "permisos"] as const,
};

function useInvalidarRoles() {
  const cliente = useQueryClient();
  return () =>
    Promise.all([
      cliente.invalidateQueries({ queryKey: rolesKeys.all }),
      cliente.invalidateQueries({ queryKey: identidadesDesarrolloKeys.all }),
      cliente.invalidateQueries({ queryKey: sesionKeys.actual }),
      cliente.invalidateQueries({ queryKey: sesionKeys.desarrollo }),
    ]);
}

export function useRoles() {
  const invalidar = useInvalidarRoles();
  return {
    consulta: useQuery({ queryKey: rolesKeys.all, queryFn: listarRoles }),
    cambiarEstado: useMutation({
      mutationFn: ({ rol, activar }: { rol: RolMock; activar: boolean }) =>
        cambiarEstadoRol(rol, activar),
      onSuccess: invalidar,
    }),
  };
}

export function useCatalogoPermisos() {
  return useQuery({ queryKey: rolesKeys.permisos, queryFn: listarPermisos });
}

interface DatosGuardarRol {
  datos: DatosRolEditables;
  permisoIds: string[];
}

const mismosIds = (a: string[], b: string[]) =>
  a.length === b.length && a.every((id) => b.includes(id));

/**
 * Alta: un único POST con los permisos iniciales. Edición: datos sólo si cambiaron y
 * después permisos sólo si cambiaron, con la versión que devolvió el paso anterior.
 */
export function useGuardarRol(rol: RolMock | null) {
  const invalidar = useInvalidarRoles();
  return useMutation({
    mutationFn: async ({ datos, permisoIds }: DatosGuardarRol) => {
      if (!rol) return crearRol(datos, permisoIds);
      let actual = rol;
      if (
        datos.nombre !== rol.nombre ||
        datos.descripcion !== rol.descripcion ||
        datos.scope !== rol.scope
      ) {
        actual = await editarRol(rol, datos);
      }
      if (
        !mismosIds(
          permisoIds,
          rol.permisos.map((p) => p.id),
        )
      ) {
        await reemplazarPermisos(actual, permisoIds);
      }
      return actual;
    },
    onSettled: invalidar,
  });
}
