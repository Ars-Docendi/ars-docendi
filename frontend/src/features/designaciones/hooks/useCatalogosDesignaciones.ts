import { useQuery } from "@tanstack/react-query";
import { obtenerCatalogosDesignaciones } from "../api/catalogos";

export const catalogosDesignacionesKeys = { all: ["designaciones", "catalogos"] as const };
export function useCatalogosDesignaciones() {
  return useQuery({
    queryKey: catalogosDesignacionesKeys.all,
    queryFn: obtenerCatalogosDesignaciones,
  });
}
