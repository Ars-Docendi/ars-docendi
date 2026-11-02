import { useQuery } from "@tanstack/react-query";
import { obtenerCatalogosDesignaciones } from "../api/catalogos";

export const catalogosDesignacionesKeys = { all: ["designaciones", "catalogos"] as const };
export function useCatalogosDesignaciones(pedidoId?: string) {
  return useQuery({
    queryKey: pedidoId
      ? [...catalogosDesignacionesKeys.all, pedidoId]
      : catalogosDesignacionesKeys.all,
    queryFn: () => obtenerCatalogosDesignaciones(pedidoId),
  });
}
