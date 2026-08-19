import { useQuery } from "@tanstack/react-query";
import { listarIdentidadesDesarrollo } from "./identidadesApi";

export const identidadesDesarrolloKeys = {
  all: ["desarrollo", "identidades"] as const,
};

export function useIdentidadesDesarrollo() {
  return useQuery({
    queryKey: identidadesDesarrolloKeys.all,
    queryFn: listarIdentidadesDesarrollo,
    staleTime: 60_000,
  });
}
