import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  fusionarTermino,
  incorporarTermino,
  listarTerminos,
  renombrarTermino,
} from "../api/vocabularioApi";

/** Curaduría del vocabulario. Cada cambio invalida también el vocabulario y los perfiles. */
export function useVocabularioAdmin() {
  const qc = useQueryClient();
  const invalidar = () => qc.invalidateQueries({ queryKey: ["portal"] });
  return {
    consulta: useQuery({
      queryKey: ["portal", "vocabulario", "terminos"],
      queryFn: listarTerminos,
    }),
    incorporar: useMutation({ mutationFn: incorporarTermino, onSuccess: invalidar }),
    renombrar: useMutation({
      mutationFn: ({ id, termino }: { id: string; termino: string }) =>
        renombrarTermino(id, termino),
      onSuccess: invalidar,
    }),
    fusionar: useMutation({
      mutationFn: ({ id, destinoId }: { id: string; destinoId: string }) =>
        fusionarTermino(id, destinoId),
      onSuccess: invalidar,
    }),
  };
}
