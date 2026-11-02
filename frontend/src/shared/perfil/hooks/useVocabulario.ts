import { useQuery } from "@tanstack/react-query";
import { listarVocabulario } from "../api/portalApi";

/** Vocabulario compartido; vacío mientras carga, así el docente igual puede sugerir. */
export function useVocabulario(): string[] {
  return useQuery({ queryKey: ["portal", "vocabulario"], queryFn: listarVocabulario }).data ?? [];
}
