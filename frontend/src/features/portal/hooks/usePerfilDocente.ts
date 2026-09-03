import { useCallback, useEffect, useState } from "react";

import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { obtenerPerfilInstitucional, perfilSeed, perfilVacio } from "../mock/mockStore";
import type { EstadoPerfil, PerfilDocente } from "../types";

/** UPN del docente que tiene perfil cargado en el seed; el resto arranca vacío. */
const UPN_CON_DATOS = "marina.diaz@unlam.edu.ar";

/** Latencia simulada para que el estado Loading sea real y no decorativo. */
const LATENCIA_MS = 250;

/** Resultado de la lectura, atado al UPN que se leyó. */
interface Lectura {
  upn: string;
  perfil: PerfilDocente | null;
}

/**
 * Lee y muta el perfil del docente autenticado contra el store mock.
 *
 * El estado de carga se **deriva** de si la lectura corresponde al usuario
 * actual, en vez de setearse dentro del efecto: así un cambio de usuario
 * muestra Loading sin renders en cascada.
 *
 * TODO(backend): reemplazar por React Query contra Modules.Portal. Mientras no
 * haya servidor no hay query que cachear, así que el estado vive acá.
 */
export function usePerfilDocente() {
  const usuario = useCurrentUser();
  const [lectura, setLectura] = useState<Lectura | null>(null);
  const [guardado, setGuardado] = useState(false);

  useEffect(() => {
    let cancelado = false;
    const id = window.setTimeout(() => {
      if (cancelado) return;
      const institucional = obtenerPerfilInstitucional(usuario.upn);
      setLectura({
        upn: usuario.upn,
        perfil: institucional
          ? usuario.upn === UPN_CON_DATOS
            ? perfilSeed(institucional)
            : perfilVacio(institucional)
          : null,
      });
    }, LATENCIA_MS);
    return () => {
      cancelado = true;
      window.clearTimeout(id);
    };
  }, [usuario.upn]);

  const vigente = lectura?.upn === usuario.upn ? lectura : null;
  const estado: EstadoPerfil = !vigente ? "cargando" : vigente.perfil ? "listo" : "error";
  const perfil = vigente?.perfil ?? null;

  /** Aplica un cambio a una sección y muestra la confirmación de guardado. */
  const actualizar = useCallback((cambio: (perfil: PerfilDocente) => PerfilDocente) => {
    setLectura((actual) =>
      actual?.perfil ? { ...actual, perfil: cambio(actual.perfil) } : actual,
    );
    setGuardado(true);
  }, []);

  const ocultarAviso = useCallback(() => setGuardado(false), []);

  return { estado, perfil, guardado, actualizar, ocultarAviso };
}
