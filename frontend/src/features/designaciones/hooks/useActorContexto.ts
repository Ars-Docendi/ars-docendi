import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import type { ActorContexto, Rol } from "../types";

/**
 * Rol del circuito por código de rol. El nombre que guarda la base ("Secretaría Académica",
 * "Coordinador de Carrera") no es la etiqueta corta del circuito, y se puede renombrar: por
 * eso se mapea desde el código, que es estable.
 */
const ROL_DEL_CIRCUITO: Record<string, Rol> = {
  jefe_catedra: "Jefe de Cátedra",
  coordinador_carrera: "Coordinador",
  secretaria: "Secretaría",
  decanato: "Decanato",
  administrativo: "Administración",
};

/** Deriva el contexto del actor (rol + ámbito) del usuario activo del app shell. */
export function useActorContexto(): ActorContexto {
  const { user } = useCurrentUser();
  if (!user) throw new Error("La sesión todavía no está disponible.");
  return { rol: ROL_DEL_CIRCUITO[user.roleCode] ?? user.role, nombre: user.name };
}
