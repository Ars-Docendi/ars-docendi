import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import type { ActorTarea } from "../types";

/**
 * Deriva el actor actual (nombre + rol) directamente de `useCurrentUser`.
 * A diferencia de Designaciones, Tareas no tiene noción de "ámbito"
 * (carrera/cátedra) — el listado es el mismo para todos los roles — así
 * que no reusa `useActorContexto` de `features/designaciones` (las
 * features no se importan entre sí).
 */
export function useActorTareas(): ActorTarea {
  const { user } = useCurrentUser();
  if (!user) {
    throw new Error("No hay una sesión válida para resolver el actor de Tareas.");
  }
  return { nombre: user.name, rol: user.role };
}
