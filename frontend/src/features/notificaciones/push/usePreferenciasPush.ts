import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { mostrarToast } from "../../../shared/ui/toasts";
import {
  guardarPreferenciasPush,
  obtenerPreferenciasPush,
  type PreferenciasPush,
} from "../api/pushApi";

const clavePreferencias = ["notificaciones", "push", "preferencias"] as const;

/** Per-module push switches of the session user. Dropped from the cache when the view closes. */
export function usePreferenciasPush(habilitada: boolean) {
  const cliente = useQueryClient();
  const consulta = useQuery({
    queryKey: clavePreferencias,
    queryFn: obtenerPreferenciasPush,
    enabled: habilitada,
    gcTime: 0,
  });

  const cambiar = useMutation({
    mutationFn: (cambio: { modulo: string; habilitado: boolean }) =>
      guardarPreferenciasPush([cambio]),
    onMutate: async (cambio) => {
      await cliente.cancelQueries({ queryKey: clavePreferencias });
      const previo = cliente.getQueryData<PreferenciasPush>(clavePreferencias);
      cliente.setQueryData<PreferenciasPush>(
        clavePreferencias,
        (actual) =>
          actual && {
            modulos: actual.modulos.map((m) =>
              m.modulo === cambio.modulo ? { ...m, habilitado: cambio.habilitado } : m,
            ),
          },
      );
      return { previo };
    },
    onError: (_error, _cambio, contexto) => {
      if (contexto?.previo) cliente.setQueryData(clavePreferencias, contexto.previo);
      mostrarToast({ titulo: "No pudimos guardar el cambio", descripcion: "Probá de nuevo." });
    },
  });

  const habilitados = new Map((consulta.data?.modulos ?? []).map((m) => [m.modulo, m.habilitado]));
  return {
    cargando: consulta.isLoading,
    fallo: consulta.isError,
    reintentar: () => void consulta.refetch(),
    /** A module missing from the response counts as enabled, like the server's default. */
    estaHabilitado: (modulo: string) => habilitados.get(modulo) ?? true,
    cambiar: (modulo: string, habilitado: boolean) => cambiar.mutate({ modulo, habilitado }),
  };
}
