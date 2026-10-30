import { useMutation, useQueryClient } from "@tanstack/react-query";

import { elegirAsignacionDesarrollo, obtenerSesionDesarrollo } from "./dev/session";
import { developmentAuthEnabled } from "./developmentAuth";
import { elegirAsignacion, sesionKeys, type AsignacionApi } from "./sesionMicrosoft";

/** Cómo se muestra el ámbito de una asignación: la materia o carrera, o "Global". */
export function etiquetaAmbito(asignacion: AsignacionApi): string {
  return asignacion.ambitoNombre ?? "Global";
}

/**
 * Elige o cambia la asignación de la sesión. En desarrollo queda guardada en el
 * navegador; con Microsoft, en la cookie. Después se recargan sesión, permisos y
 * todo lo que dependía del rol anterior.
 */
export function useElegirRol() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: async (asignacionId: string) => {
      if (developmentAuthEnabled && obtenerSesionDesarrollo()) {
        elegirAsignacionDesarrollo(asignacionId);
        return;
      }
      await elegirAsignacion(asignacionId);
    },
    onSuccess: async () => {
      cliente.removeQueries({
        predicate: (q) => q.queryKey[0] !== sesionKeys.actual[0] && q.queryKey[0] !== "desarrollo",
      });
      await cliente.invalidateQueries({ queryKey: sesionKeys.actual });
      await cliente.invalidateQueries({ queryKey: sesionKeys.desarrollo });
    },
  });
}
