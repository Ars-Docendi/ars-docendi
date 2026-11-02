import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  cambiarEstadoAula,
  crearAula,
  editarAula,
  eliminarAula,
  listarAulas,
} from "../api/catalogoApi";
import type { Aula, DatosAula } from "../types";

const clave = ["aulas", "catalogo"] as const;

/** Aulas activas, para elegir al asignar una solicitud. */
export function useAulasActivas() {
  return useQuery({ queryKey: [...clave, "activas"], queryFn: () => listarAulas(true) });
}

/** Catálogo completo con sus mutaciones; cada una invalida el catálogo. */
export function useCatalogoAulas() {
  const qc = useQueryClient();
  const invalidar = () => qc.invalidateQueries({ queryKey: clave });
  return {
    consulta: useQuery({ queryKey: [...clave, "todas"], queryFn: () => listarAulas() }),
    guardar: useMutation({
      mutationFn: ({ id, datos }: { id?: string; datos: DatosAula }) =>
        id ? editarAula(id, datos) : crearAula(datos),
      onSuccess: invalidar,
    }),
    cambiarEstado: useMutation({
      mutationFn: ({ aula, activa }: { aula: Aula; activa: boolean }) =>
        cambiarEstadoAula(aula, activa),
      onSuccess: invalidar,
    }),
    eliminar: useMutation({ mutationFn: eliminarAula, onSuccess: invalidar }),
  };
}
