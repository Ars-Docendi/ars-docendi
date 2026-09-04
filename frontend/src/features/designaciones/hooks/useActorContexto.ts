import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import type { ActorContexto } from "../types";

/** Deriva el contexto del actor (rol + ámbito) del usuario activo del app shell. */
export function useActorContexto(): ActorContexto {
  const { user } = useCurrentUser();
  if (!user) throw new Error("La sesión todavía no está disponible.");
  return { rol: user.role, nombre: user.name };
}
