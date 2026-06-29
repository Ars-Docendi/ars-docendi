import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { construirActorContexto } from "../api/contextoActor";
import type { ActorContexto } from "../types";

/** Deriva el contexto del actor (rol + ámbito) del usuario activo del app shell. */
export function useActorContexto(): ActorContexto {
  const usuario = useCurrentUser();
  return construirActorContexto(usuario.role, usuario.name);
}
