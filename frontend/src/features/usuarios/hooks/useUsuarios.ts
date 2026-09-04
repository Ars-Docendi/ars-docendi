import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  cambiarEstadoUsuario,
  crearUsuario,
  editarUsuario,
  listarUsuarios,
  obtenerCatalogosUsuarios,
} from "../api/usuariosApi";
import type { UsuarioMock } from "../models";

export const usuariosKeys = {
  all: ["administracion", "usuarios"] as const,
  catalogos: ["administracion", "catalogos"] as const,
};

export function useUsuarios() {
  const cliente = useQueryClient();
  const usuarios = useQuery({ queryKey: usuariosKeys.all, queryFn: listarUsuarios });
  const catalogos = useQuery({
    queryKey: usuariosKeys.catalogos,
    queryFn: obtenerCatalogosUsuarios,
  });
  const invalidar = () => cliente.invalidateQueries({ queryKey: usuariosKeys.all });
  return {
    usuarios,
    catalogos,
    crear: useMutation({
      mutationFn: (datos: Parameters<typeof crearUsuario>[0]) => {
        if (!catalogos.data) throw new Error("Los catálogos todavía no están disponibles.");
        return crearUsuario(datos, catalogos.data);
      },
      onSuccess: invalidar,
    }),
    editar: useMutation({
      mutationFn: ({ id, datos }: { id: string; datos: Parameters<typeof editarUsuario>[1] }) => {
        if (!catalogos.data) throw new Error("Los catálogos todavía no están disponibles.");
        return editarUsuario(id, datos, catalogos.data);
      },
      onSuccess: invalidar,
    }),
    cambiarEstado: useMutation({
      mutationFn: ({ usuario, activo }: { usuario: UsuarioMock; activo: boolean }) =>
        cambiarEstadoUsuario(usuario, activo),
      onSuccess: invalidar,
    }),
  };
}
