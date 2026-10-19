import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  importarComisiones,
  listarComisiones,
  listarPeriodosGuarani,
  listarPeriodosImportados,
} from "../api/comisionesApi";

export const comisionesKeys = {
  all: ["designaciones", "comisiones"] as const,
  periodos: ["designaciones", "comisiones", "periodos"] as const,
  periodo: (id: string) => ["designaciones", "comisiones", "periodo", id] as const,
  guarani: ["designaciones", "comisiones", "guarani"] as const,
};

export function usePeriodosImportados() {
  return useQuery({ queryKey: comisionesKeys.periodos, queryFn: () => listarPeriodosImportados() });
}

export function useComisionesDelPeriodo(periodoLectivoId: string | undefined) {
  return useQuery({
    queryKey: comisionesKeys.periodo(periodoLectivoId ?? ""),
    queryFn: () => listarComisiones(periodoLectivoId!),
    enabled: Boolean(periodoLectivoId),
  });
}

/** Períodos de Guaraní: sólo se piden con el modal abierto, nunca al cargar la pantalla. */
export function usePeriodosGuarani(habilitado: boolean) {
  return useQuery({
    queryKey: comisionesKeys.guarani,
    queryFn: () => listarPeriodosGuarani(),
    enabled: habilitado,
    retry: false,
  });
}

export function useImportarComisiones() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (periodoLectivoId: string) => importarComisiones(periodoLectivoId),
    onSettled: () => cliente.invalidateQueries({ queryKey: comisionesKeys.all }),
  });
}
