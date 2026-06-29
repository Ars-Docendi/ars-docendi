// ============================================================
// DEV MOCK LOGIN — remove with shared/auth/dev/
// ------------------------------------------------------------
// Persists which mock user was picked in the login modal, kept
// separate from the real auth token in ../auth.ts. useCurrentUser()
// reads getMockUser() to render the app as the chosen role.
//
// SCRUM-8 — Role switching: un usuario multi-rol (p. ej. "Demo (todos
// los roles)") puede cambiar el ROL ACTIVO sin re-loguear. El rol activo
// se persiste aparte (`adoc.dev.mockRol`) y un mini pub-sub notifica a
// useCurrentUser (vía useSyncExternalStore) para que toda la app —incluido
// el ActorContexto que consume la capa api/— reaccione al cambio.
// ============================================================
import type { CurrentUser, Role } from "../useCurrentUser";
import { MOCK_USERS } from "./mockUsers";

const MOCK_USER_KEY = "adoc.dev.mockUser";
const MOCK_ROL_KEY = "adoc.dev.mockRol";

// --- Pub-sub mínimo para que useCurrentUser observe los cambios ---
const oyentes = new Set<() => void>();

/** Suscribe un listener a los cambios de la sesión mock. Devuelve la baja. */
export function suscribirMockSession(cb: () => void): () => void {
  oyentes.add(cb);
  return () => oyentes.delete(cb);
}

function notificar(): void {
  oyentes.forEach((cb) => cb());
}

// --- Snapshot memoizado (identidad estable mientras no cambie la sesión) ---
// useSyncExternalStore exige que getSnapshot devuelva la MISMA referencia si
// nada cambió; por eso cacheamos por la clave `${userId}|${rolActivo}`.
let snapshotClave: string | null = null;
let snapshot: CurrentUser | null = null;

function recomputarSnapshot(): CurrentUser | null {
  const id = localStorage.getItem(MOCK_USER_KEY);
  const rolActivo = localStorage.getItem(MOCK_ROL_KEY);
  const clave = `${id}|${rolActivo}`;
  if (clave === snapshotClave) {
    return snapshot;
  }
  snapshotClave = clave;
  const base = id ? (MOCK_USERS.find((u) => u.id === id)?.currentUser ?? null) : null;
  if (base && rolActivo && base.roles.includes(rolActivo as Role)) {
    // Override del rol activo, acotado a los roles que el usuario puede asumir.
    snapshot = { ...base, role: rolActivo as Role };
  } else {
    snapshot = base;
  }
  return snapshot;
}

export function setMockUser(id: string): void {
  localStorage.setItem(MOCK_USER_KEY, id);
  // Al elegir un usuario, el rol activo arranca en su rol por defecto.
  localStorage.removeItem(MOCK_ROL_KEY);
  notificar();
}

export function clearMockUser(): void {
  localStorage.removeItem(MOCK_USER_KEY);
  localStorage.removeItem(MOCK_ROL_KEY);
  notificar();
}

/** Fija el rol activo de un usuario multi-rol (role switching sin re-loguear). */
export function setRolActivo(rol: Role): void {
  localStorage.setItem(MOCK_ROL_KEY, rol);
  notificar();
}

/** El rol activo persistido, o null si no se eligió ninguno (usa el del usuario). */
export function getRolActivo(): Role | null {
  return localStorage.getItem(MOCK_ROL_KEY) as Role | null;
}

/** The selected mock user as a CurrentUser (rol activo aplicado), or null if none chosen. */
export function getMockUser(): CurrentUser | null {
  return recomputarSnapshot();
}
