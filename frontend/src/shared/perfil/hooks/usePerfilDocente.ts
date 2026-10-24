import { useCallback, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";

import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { mensajeProblema } from "../../../shared/api/problemDetails";
import {
  type ApiPerfil,
  crearCertificacion,
  crearEducacion,
  crearExperiencia,
  crearProyecto,
  editarCertificacion,
  editarEducacion,
  editarExperiencia,
  editarProyecto,
  eliminarCertificacion,
  eliminarEducacion,
  eliminarExperiencia,
  eliminarProyecto,
  eliminarCv,
  guardarContacto,
  guardarCv,
  obtenerPerfil,
  perfilKey,
  reemplazarTags,
  descargarCv,
  descargarDocumento,
} from "../api/portalApi";
import type { EstadoPerfil, PerfilDocente } from "../types";

/** Perfil propio (Mi Portal). Las funciones se leen del módulo para que los tests puedan reemplazarlas. */
export function usePerfilDocente() {
  const usuario = useCurrentUser();
  const upn = usuario.user?.upn ?? "";
  const api: ApiPerfil = {
    obtenerPerfil,
    guardarContacto,
    guardarCv,
    eliminarCv,
    descargarCv,
    descargarDocumento,
    reemplazarTags,
    crearExperiencia,
    editarExperiencia,
    eliminarExperiencia,
    crearEducacion,
    editarEducacion,
    eliminarEducacion,
    crearCertificacion,
    editarCertificacion,
    eliminarCertificacion,
    crearProyecto,
    editarProyecto,
    eliminarProyecto,
  };
  return usePerfil(api, [...perfilKey, upn], perfilKey);
}

/**
 * Perfil de Mi Portal con guardado por sección: aplica el cambio de forma optimista y sincroniza
 * con la API indicada (propia o de otra persona).
 */
export function usePerfil(
  api: ApiPerfil,
  clave: readonly unknown[],
  prefijoInvalidacion: readonly unknown[] = clave,
) {
  const cliente = useQueryClient();
  const [guardado, setGuardado] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const consulta = useQuery({
    queryKey: clave,
    queryFn: () => api.obtenerPerfil(),
    retry: false,
  });
  const actualizar = useCallback(
    (cambio: (perfil: PerfilDocente) => PerfilDocente) => {
      if (!consulta.data) return;
      const anterior = consulta.data;
      const siguiente = cambio(anterior);
      cliente.setQueryData(clave, siguiente);
      setGuardado(false);
      setError(null);
      void Promise.all([
        siguiente.contacto !== anterior.contacto
          ? api.guardarContacto({ ...siguiente.contacto, version: anterior.contacto.version })
          : undefined,
        undefined,
        sincronizar(
          anterior.experiencia,
          siguiente.experiencia,
          api.crearExperiencia,
          api.editarExperiencia,
          api.eliminarExperiencia,
        ),
        sincronizar(
          anterior.educacion,
          siguiente.educacion,
          api.crearEducacion,
          api.editarEducacion,
          api.eliminarEducacion,
        ),
        sincronizar(
          anterior.certificaciones,
          siguiente.certificaciones,
          api.crearCertificacion,
          api.editarCertificacion,
          api.eliminarCertificacion,
        ),
        sincronizar(
          anterior.proyectos,
          siguiente.proyectos,
          api.crearProyecto,
          api.editarProyecto,
          api.eliminarProyecto,
        ),
        siguiente.habilidades !== anterior.habilidades
          ? api.reemplazarTags(
              "habilidades",
              siguiente.habilidades.map((x) => x.termino),
              anterior.versionHabilidades,
            )
          : undefined,
        siguiente.intereses !== anterior.intereses
          ? api.reemplazarTags(
              "intereses",
              siguiente.intereses.map((x) => x.termino),
              anterior.versionIntereses,
            )
          : undefined,
      ])
        .then(async () => {
          await cliente.invalidateQueries({ queryKey: prefijoInvalidacion });
          setGuardado(true);
        })
        .catch((causa) => {
          cliente.setQueryData(clave, anterior);
          void cliente.invalidateQueries({ queryKey: prefijoInvalidacion });
          setError(mensajeProblema(causa, "No se pudieron guardar los cambios."));
        });
    },
    [api, cliente, clave, consulta.data, prefijoInvalidacion],
  );
  const estado: EstadoPerfil = consulta.isPending
    ? "cargando"
    : consulta.isError
      ? "error"
      : "listo";
  const cargarCv = useCallback(
    async (archivo: File) => {
      if (!consulta.data) return;
      const anterior = consulta.data;
      const pendiente = {
        archivoId: `pendiente-${crypto.randomUUID()}`,
        nombre: archivo.name,
        fechaCarga: new Date().toISOString().slice(0, 10),
        tamanoBytes: archivo.size,
        estado: "pendiente",
      };
      cliente.setQueryData(clave, { ...anterior, cv: pendiente });
      try {
        setError(null);
        await api.guardarCv(archivo, anterior.cv?.version);
        await cliente.invalidateQueries({ queryKey: prefijoInvalidacion });
        setGuardado(true);
      } catch (causa) {
        cliente.setQueryData(clave, anterior);
        void cliente.invalidateQueries({ queryKey: prefijoInvalidacion });
        setError(mensajeProblema(causa, "No se pudo confirmar el CV."));
      }
    },
    [api, cliente, clave, consulta.data, prefijoInvalidacion],
  );
  const borrarCv = useCallback(async () => {
    try {
      setError(null);
      await api.eliminarCv(consulta.data?.cv?.version);
      await cliente.invalidateQueries({ queryKey: prefijoInvalidacion });
      setGuardado(true);
    } catch (causa) {
      void cliente.invalidateQueries({ queryKey: prefijoInvalidacion });
      setError(mensajeProblema(causa, "No se pudo eliminar el CV."));
    }
  }, [api, cliente, consulta.data, prefijoInvalidacion]);
  return {
    estado,
    perfil: consulta.data ?? null,
    guardado,
    error,
    actualizar,
    cargarCv,
    borrarCv,
    ocultarAviso: () => setGuardado(false),
  };
}

async function sincronizar<T extends { id: string; version?: number | null }>(
  anterior: T[],
  siguiente: T[],
  crear: (datos: Omit<T, "id">) => Promise<unknown>,
  editar: (id: string, datos: Omit<T, "id">) => Promise<unknown>,
  eliminar: (id: string, version?: number | null) => Promise<void>,
) {
  await Promise.all([
    ...siguiente
      .filter((item) => anterior.find((x) => x.id === item.id) !== item)
      .map(({ id, ...datos }) =>
        anterior.some((x) => x.id === id)
          ? editar(id, { ...datos, version: anterior.find((x) => x.id === id)?.version })
          : crear(datos),
      ),
    ...anterior
      .filter((item) => !siguiente.some((x) => x.id === item.id))
      .map((item) => eliminar(item.id, item.version)),
  ]);
}
