import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  crearRol,
  editarRol,
  listarPermisos,
  listarRoles,
  obtenerPermisosRol,
  reemplazarPermisos,
} from "../api/rolesApi";
import type { DatosRolEditables, DatosRolNuevo, RolMock } from "../models";

export const rolesKeys = {
  all: ["administracion", "roles"] as const,
  permisos: ["administracion", "permisos"] as const,
  permisosRol: (id: string) => ["administracion", "roles", id, "permisos"] as const,
};
export function useRoles() {
  const cliente = useQueryClient();
  const invalidar = () => cliente.invalidateQueries({ queryKey: rolesKeys.all });
  return {
    consulta: useQuery({ queryKey: rolesKeys.all, queryFn: listarRoles }),
    crear: useMutation({
      mutationFn: ({ datos, rolBaseId }: { datos: DatosRolNuevo; rolBaseId: string | null }) =>
        crearRol(datos, rolBaseId),
      onSuccess: invalidar,
    }),
    editar: useMutation({
      mutationFn: ({ rol, datos }: { rol: RolMock; datos: DatosRolEditables }) =>
        editarRol(rol, datos),
      onSuccess: invalidar,
    }),
  };
}
export function useMembresiaRol(rol: RolMock | null) {
  const cliente = useQueryClient();
  const catalogo = useQuery({ queryKey: rolesKeys.permisos, queryFn: listarPermisos });
  const asignados = useQuery({
    queryKey: rolesKeys.permisosRol(rol?.id ?? ""),
    queryFn: () => obtenerPermisosRol(rol!.id),
    enabled: rol !== null,
  });
  const guardar = useMutation({
    mutationFn: (ids: string[]) => reemplazarPermisos(rol!, ids),
    onSuccess: async () => {
      await cliente.invalidateQueries({ queryKey: rolesKeys.permisosRol(rol?.id ?? "") });
      await cliente.invalidateQueries({ queryKey: rolesKeys.all });
    },
  });
  return { catalogo, asignados, guardar };
}
