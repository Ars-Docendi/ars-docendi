import { useCallback, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";

import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { mensajeProblema } from "../../../shared/api/problemDetails";
import {
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
} from "../api/portalApi";
import type { EstadoPerfil, PerfilDocente } from "../types";

export function usePerfilDocente() {
  const usuario = useCurrentUser();
  const upn = usuario.user?.upn ?? "";
  const cliente = useQueryClient();
  const [guardado, setGuardado] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const consulta = useQuery({
    queryKey: [...perfilKey, upn],
    queryFn: obtenerPerfil,
    retry: false,
  });
  const actualizar = useCallback(
    (cambio: (perfil: PerfilDocente) => PerfilDocente) => {
      if (!consulta.data) return;
      const anterior = consulta.data;
      const siguiente = cambio(anterior);
      cliente.setQueryData([...perfilKey, upn], siguiente);
      setGuardado(false);
      setError(null);
      void Promise.all([
        siguiente.contacto !== anterior.contacto ? guardarContacto(siguiente.contacto) : undefined,
        undefined,
        sincronizar(
          anterior.experiencia,
          siguiente.experiencia,
          crearExperiencia,
          editarExperiencia,
          eliminarExperiencia,
        ),
        sincronizar(
          anterior.educacion,
          siguiente.educacion,
          crearEducacion,
          editarEducacion,
          eliminarEducacion,
        ),
        sincronizar(
          anterior.certificaciones,
          siguiente.certificaciones,
          crearCertificacion,
          editarCertificacion,
          eliminarCertificacion,
        ),
        sincronizar(
          anterior.proyectos,
          siguiente.proyectos,
          crearProyecto,
          editarProyecto,
          eliminarProyecto,
        ),
        siguiente.habilidades !== anterior.habilidades
          ? reemplazarTags(
              "habilidades",
              siguiente.habilidades.map((x) => x.termino),
            )
          : undefined,
        siguiente.intereses !== anterior.intereses
          ? reemplazarTags(
              "intereses",
              siguiente.intereses.map((x) => x.termino),
            )
          : undefined,
      ])
        .then(async () => {
          await cliente.invalidateQueries({ queryKey: perfilKey });
          setGuardado(true);
        })
        .catch((causa) => {
          cliente.setQueryData([...perfilKey, upn], anterior);
          setError(mensajeProblema(causa, "No se pudieron guardar los cambios."));
        });
    },
    [cliente, consulta.data, upn],
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
      cliente.setQueryData([...perfilKey, upn], { ...anterior, cv: pendiente });
      try {
        setError(null);
        await guardarCv(archivo);
        await cliente.invalidateQueries({ queryKey: perfilKey });
        setGuardado(true);
      } catch (causa) {
        cliente.setQueryData([...perfilKey, upn], anterior);
        setError(mensajeProblema(causa, "No se pudo confirmar el CV."));
      }
    },
    [cliente, consulta.data, upn],
  );
  const borrarCv = useCallback(async () => {
    try {
      setError(null);
      await eliminarCv();
      await cliente.invalidateQueries({ queryKey: perfilKey });
      setGuardado(true);
    } catch (causa) {
      setError(mensajeProblema(causa, "No se pudo eliminar el CV."));
    }
  }, [cliente]);
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

async function sincronizar<T extends { id: string }>(
  anterior: T[],
  siguiente: T[],
  crear: (datos: Omit<T, "id">) => Promise<unknown>,
  editar: (id: string, datos: Omit<T, "id">) => Promise<unknown>,
  eliminar: (id: string) => Promise<void>,
) {
  await Promise.all([
    ...siguiente
      .filter((item) => anterior.find((x) => x.id === item.id) !== item)
      .map(({ id, ...datos }) =>
        anterior.some((x) => x.id === id) ? editar(id, datos) : crear(datos),
      ),
    ...anterior
      .filter((item) => !siguiente.some((x) => x.id === item.id))
      .map((item) => eliminar(item.id)),
  ]);
}
