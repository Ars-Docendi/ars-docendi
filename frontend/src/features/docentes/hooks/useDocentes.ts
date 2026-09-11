import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  cambiarEstadoDocente,
  crearDocente,
  editarDocente,
  listarDocentes,
  obtenerCatalogosDocentes,
} from "../api/docentesApi";
import type { DocenteMock } from "../models";

export const docentesKeys = {
  all: ["administracion", "docentes"] as const,
  catalogos: ["administracion", "docentes", "catalogos"] as const,
};
export function useDocentes() {
  const cliente = useQueryClient();
  const consulta = useQuery({ queryKey: docentesKeys.all, queryFn: listarDocentes });
  const catalogos = useQuery({
    queryKey: docentesKeys.catalogos,
    queryFn: obtenerCatalogosDocentes,
  });
  const invalidar = () => cliente.invalidateQueries({ queryKey: docentesKeys.all });
  return {
    consulta,
    catalogos,
    crear: useMutation({
      mutationFn: (datos: Omit<DocenteMock, "id" | "is_active">) => {
        if (!catalogos.data) throw new Error("Los catálogos todavía no están disponibles.");
        return crearDocente(datos, catalogos.data);
      },
      onSuccess: invalidar,
    }),
    editar: useMutation({
      mutationFn: ({
        docente,
        datos,
      }: {
        docente: DocenteMock;
        datos: Omit<DocenteMock, "id" | "is_active">;
      }) => {
        if (!catalogos.data) throw new Error("Los catálogos todavía no están disponibles.");
        return editarDocente(docente, datos, catalogos.data);
      },
      onSuccess: invalidar,
    }),
    cambiarEstado: useMutation({
      mutationFn: ({ docente, activo }: { docente: DocenteMock; activo: boolean }) =>
        cambiarEstadoDocente(docente, activo),
      onSuccess: invalidar,
    }),
  };
}
