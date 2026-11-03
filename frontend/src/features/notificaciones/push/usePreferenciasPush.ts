import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { mostrarToast } from "../../../shared/ui/toasts";
import {
  guardarPreferenciasPush,
  obtenerPreferenciasPush,
  type PreferenciasPush,
} from "../api/pushApi";

const clavePreferencias = ["notificaciones", "push", "preferencias"] as const;

/** Per-type push switches of the session user. Dropped from the cache when the view closes. */
export function usePreferenciasPush(habilitada: boolean) {
  const cliente = useQueryClient();
  const consulta = useQuery({
    queryKey: clavePreferencias,
    queryFn: obtenerPreferenciasPush,
    enabled: habilitada,
    gcTime: 0,
  });

  const cambiar = useMutation({
    mutationFn: (cambio: { tipoEvento: string; habilitado: boolean }) =>
      guardarPreferenciasPush([cambio]),
    onMutate: async (cambio) => {
      await cliente.cancelQueries({ queryKey: clavePreferencias });
      const previo = cliente.getQueryData<PreferenciasPush>(clavePreferencias);
      cliente.setQueryData<PreferenciasPush>(
        clavePreferencias,
        (actual) =>
          actual && {
            tipos: actual.tipos.map((t) =>
              t.tipoEvento === cambio.tipoEvento ? { ...t, habilitado: cambio.habilitado } : t,
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

  const habilitados = new Map(
    (consulta.data?.tipos ?? []).map((t) => [t.tipoEvento, t.habilitado]),
  );
  return {
    cargando: consulta.isLoading,
    fallo: consulta.isError,
    reintentar: () => void consulta.refetch(),
    /** A type missing from the response counts as enabled, like the server's default. */
    estaHabilitado: (tipoEvento: string) => habilitados.get(tipoEvento) ?? true,
    cambiar: (tipoEvento: string, habilitado: boolean) =>
      cambiar.mutate({ tipoEvento, habilitado }),
  };
}
