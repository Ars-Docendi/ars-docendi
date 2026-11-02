import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  asignarAula,
  cancelarSolicitud,
  crearSolicitud,
  rechazarSolicitud,
} from "../api/solicitudesApi";
import type { DatosNuevaSolicitud } from "../types";

/** Crea una solicitud de reserva de aula. Invalida las listas al terminar. */
export function useCrearSolicitud() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (datos: DatosNuevaSolicitud) => crearSolicitud(datos),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["aulas", "solicitudes"] }),
  });
}

/** Cancela una solicitud propia en estado Pendiente. */
export function useCancelarSolicitud() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => cancelarSolicitud(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["aulas", "solicitudes"] }),
  });
}

/** Asigna un aula a una solicitud Pendiente, aprobándola. */
export function useAsignarAula() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, aulaAsignada }: { id: string; aulaAsignada: string }) =>
      asignarAula(id, aulaAsignada),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["aulas", "solicitudes"] }),
  });
}

/** Rechaza una solicitud Pendiente indicando un motivo obligatorio. */
export function useRechazarSolicitud() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, motivo }: { id: string; motivo: string }) => rechazarSolicitud(id, motivo),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["aulas", "solicitudes"] }),
  });
}
