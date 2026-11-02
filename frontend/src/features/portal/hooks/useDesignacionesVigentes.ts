import { useQuery } from "@tanstack/react-query";
import { listarDesignacionesVigentes } from "../api/designacionesApi";

export function useDesignacionesVigentes() {
  return useQuery({
    queryKey: ["designaciones", "vigentes", "mias"],
    queryFn: listarDesignacionesVigentes,
  });
}
