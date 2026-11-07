import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  consultarConciliacion,
  rechazarEnConciliacion,
  sincronizarConciliacion,
} from "../api/conciliacionApi";

const CLAVE = ["conciliacion"] as const;

/** Pedidos en lote, rechazados y sincronizados del período activo, en el ámbito del actor. */
export function useConciliacion() {
  return useQuery({ queryKey: CLAVE, queryFn: consultarConciliacion });
}

function useInvalidar() {
  const qc = useQueryClient();
  return () =>
    Promise.all([
      qc.invalidateQueries({ queryKey: CLAVE }),
      qc.invalidateQueries({ queryKey: ["pedidos"] }),
    ]);
}

/** La clave de idempotencia la fija quien abre el preview: un reintento no aplica dos veces. */
export function useSincronizarConciliacion() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: ({
      pedidos,
      clave,
    }: {
      pedidos: { id: string; version: number }[];
      clave: string;
    }) => sincronizarConciliacion(pedidos, clave),
    onSettled: invalidar,
  });
}

export function useRechazarEnConciliacion() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: ({ id, motivo }: { id: string; motivo: string }) =>
      rechazarEnConciliacion(id, motivo),
    onSettled: invalidar,
  });
}
