import { useQuery } from "@tanstack/react-query";
import {
  listarMateriasPropias,
  listarMisSolicitudes,
  listarTodasLasSolicitudes,
} from "../api/solicitudesApi";

/** Materias asignadas al Docente autenticado, para el desplegable de "Nueva solicitud". */
export function useMateriasPropias() {
  return useQuery({
    queryKey: ["aulas", "materias-propias"],
    queryFn: listarMateriasPropias,
  });
}

/** Solicitudes de reserva de aula propias del Docente autenticado. */
export function useMisSolicitudes() {
  return useQuery({
    queryKey: ["aulas", "solicitudes", "mias"],
    queryFn: listarMisSolicitudes,
  });
}

/** Todas las solicitudes de reserva de aula (vista Administrativo). */
export function useTodasLasSolicitudes() {
  return useQuery({
    queryKey: ["aulas", "solicitudes", "todas"],
    queryFn: listarTodasLasSolicitudes,
  });
}
