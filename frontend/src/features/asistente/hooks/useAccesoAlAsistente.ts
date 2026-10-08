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

/**
 * Decide si este usuario puede usar el asistente PREGUNTÁNDOLE AL BACKEND.
 *
 * Lo tentador sería una lista de roles —«todos menos Docente»—, y es el mismo
 * antipatrón que el backend rechazó al sembrar el permiso: `identity.roles` NO es un
 * catálogo cerrado, Secretaría puede crear roles nuevos desde la aplicación, y una
 * lista embebida acá falla ABIERTA con cualquier rol que no conozca. El fallo no
 * daría error: le mostraría el asistente a alguien que no debería verlo.
 *
 * `GET /api/asistente/capacidades` responde 403 a quien no tiene el permiso, así que
 * el gate es el permiso real, resuelto por quien lo administra.
 *
 * El catálogo que trae de vuelta no se descarta: es exactamente lo que la vista
 * necesita para su pantalla inicial, así que la consulta no es un costo extra.
 */
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
    // Un 403 es una respuesta: no tiene acceso. Cualquier otro fallo es una
    // incógnita, y ante la duda no se muestra: prometer una función que no
    // responde es peor que no ofrecerla.
    return { tieneAcceso: false, capacidades: undefined, cargando: false };
  }

  return { tieneAcceso: true, capacidades: consulta.data, cargando: false };
}

function esNegado(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 403;
}
