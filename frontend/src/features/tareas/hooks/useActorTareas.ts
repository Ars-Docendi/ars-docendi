import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import type { PersonaTarea } from "../types";

/**
 * Deriva el actor actual (nombre + rol) directamente de `useCurrentUser`.
 * A diferencia de Designaciones, Tareas no tiene noción de "ámbito"
 * (carrera/cátedra) — el listado es el mismo para todos los roles — así
 * que no reusa `useActorContexto` de `features/designaciones` (las
 * features no se importan entre sí).
 */
export function useActorTareas(): PersonaTarea {
  const { user } = useCurrentUser();
  if (!user) {
    throw new Error("No hay una sesión válida para resolver el actor de Tareas.");
  }
  return { id: user.id, nombre: user.name, rol: user.role };
}

/**
 * Qué puede hacer el usuario en Tareas, según los permisos de su rol vigente (no su nombre de
 * rol): `tareas.gestionar` crea tareas, `proyectos.gestionar` crea y gestiona proyectos.
 * Ver el listado (`tareas.ver`) lo controla el menú lateral. El servidor vuelve a validar todo.
 */
export function usePermisosTareas(): {
  puedeCrearTarea: boolean;
  puedeGestionarProyectos: boolean;
} {
  const { user } = useCurrentUser();
  const permisos = user?.permissions ?? [];
  return {
    puedeCrearTarea: permisos.includes("tareas.gestionar"),
    puedeGestionarProyectos: permisos.includes("proyectos.gestionar"),
  };
}
