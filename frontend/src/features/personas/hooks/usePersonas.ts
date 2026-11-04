import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import {
  cambiarEstadoPersona,
  crearPersona,
  editarPersona,
  listarPersonas,
  obtenerCatalogosPersonas,
  obtenerPersona,
  personasKeys,
  type GuardarPersona,
  type Persona,
} from "../api/personasApi";

/** Qué puede hacer el operador; el backend valida lo mismo en cada operación. */
export function usePermisosPersonas() {
  const permisos = useCurrentUser().user?.permissions ?? [];
  return {
    administrar: permisos.includes("personas.administrar"),
    administrarPortal: permisos.includes("portal.administrar"),
  };
}

export function usePersonas() {
  const cliente = useQueryClient();
  const consulta = useQuery({ queryKey: personasKeys.todas, queryFn: listarPersonas });
  const cambiarEstado = useMutation({
    mutationFn: ({ persona, activar }: { persona: Persona; activar: boolean }) =>
      cambiarEstadoPersona(persona, activar),
    onSuccess: () => cliente.invalidateQueries({ queryKey: personasKeys.todas }),
  });
  return { consulta, cambiarEstado };
}

export function useCatalogosPersonas() {
  return useQuery({ queryKey: personasKeys.catalogos, queryFn: obtenerCatalogosPersonas });
}

export function usePersona(id: string | undefined) {
  return useQuery({
    queryKey: personasKeys.detalle(id ?? "nueva"),
    queryFn: () => obtenerPersona(id!),
    enabled: Boolean(id),
  });
}

export function useGuardarPersona(id: string | undefined) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (datos: GuardarPersona) => (id ? editarPersona(id, datos) : crearPersona(datos)),
    onSuccess: () => cliente.invalidateQueries({ queryKey: personasKeys.todas }),
  });
}
