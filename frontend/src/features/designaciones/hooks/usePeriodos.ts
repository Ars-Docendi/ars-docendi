import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { crearPeriodo, editarPeriodo, eliminarPeriodo, listarPeriodos } from "../api/periodosApi";
import type { PeriodoDesignacion } from "../types";

export const periodosKeys = { all: ["designaciones", "periodos"] as const };
export function usePeriodos() {
  const cliente = useQueryClient();
  const invalidar = () => cliente.invalidateQueries({ queryKey: periodosKeys.all });
  return {
    consulta: useQuery({ queryKey: periodosKeys.all, queryFn: listarPeriodos }),
    crear: useMutation({ mutationFn: crearPeriodo, onSuccess: invalidar }),
    editar: useMutation({
      mutationFn: ({ id, datos }: { id: string; datos: Omit<PeriodoDesignacion, "id"> }) =>
        editarPeriodo(id, datos),
      onSuccess: invalidar,
    }),
    eliminar: useMutation({ mutationFn: eliminarPeriodo, onSuccess: invalidar }),
  };
}
