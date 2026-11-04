import { useQuery } from "@tanstack/react-query";
import axios from "axios";

import { obtenerCapacidades } from "../api/asistenteApi";
import type { CapacidadesDelAsistente } from "../types";

export interface AccesoAlAsistente {
  /** `undefined` mientras no se sabe: ni se muestra ni se oculta todavía. */
  tieneAcceso: boolean | undefined;
  capacidades: CapacidadesDelAsistente | undefined;
  cargando: boolean;
}

/** Decide si este usuario puede usar el asistente PREGUNTÁNDOLE AL BACKEND. */
export function useAccesoAlAsistente(): AccesoAlAsistente {
  const consulta = useQuery({
    queryKey: ["asistente", "capacidades"],
    queryFn: obtenerCapacidades,
    // Los GRANT no cambian mientras el usuario navega, y un 403 tampoco se
    // convierte en un 200 reintentando.
    staleTime: 5 * 60 * 1000,
    retry: (intentos, error) => !esNegado(error) && intentos < 2,
  });

  if (consulta.isPending) {
    return { tieneAcceso: undefined, capacidades: undefined, cargando: true };
  }

  if (consulta.isError) {
    // Un 403 es una respuesta: no tiene acceso. Cualquier otro fallo es una incógnita, y ante la
    // duda no se muestra: prometer una función que no responde es peor que no ofrecerla.
    return { tieneAcceso: false, capacidades: undefined, cargando: false };
  }

  return { tieneAcceso: true, capacidades: consulta.data, cargando: false };
}

function esNegado(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 403;
}
