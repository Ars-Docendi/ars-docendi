import { useQuery } from "@tanstack/react-query";

import { obtenerCalidad } from "../api/administracionAsistenteApi";
import { CLAVE_CALIDAD } from "../utils/claveDeCalidad";

/**
 * `true` when the 30-day negative rate is above the previous period's: it turns the «Calidad»
 * tab's status dot to warning.
 */
export function useCalidadEnAlza(habilitado: boolean): boolean {
  const { data } = useQuery({
    queryKey: [...CLAVE_CALIDAD, "agregados", 30, ""],
    queryFn: () => obtenerCalidad(30),
    enabled: habilitado,
  });
  return (data?.tendencia?.variacion ?? 0) > 0;
}
