// ============================================================
// Current user / role source for the app shell.
// STUB: returns a fixed user. Replace with Azure AD / MSAL claims
// (name, roles) once SSO is wired — mirrors the dev token stub in
// auth.ts. The returned `role` drives which sidebar nav renders
// and what the topbar RoleBadge shows.
// ============================================================

export type Role =
  | "Jefe de Cátedra"
  | "Coordinador"
  | "Secretaría"
  | "Decanato"
  | "Administración"
  | "Docente";

export interface CurrentUser {
  /** Display name, e.g. "G. Ruiz". */
  name: string;
  /** 1–2 letters for the avatar circle. */
  initials: string;
  /** The role currently in effect. */
  role: Role;
  /** Every role this account may act as (for the role switcher). */
  roles: Role[];
}

const STUB_USER: CurrentUser = {
  name: "G. Ruiz",
  initials: "GR",
  role: "Jefe de Cátedra",
  roles: ["Jefe de Cátedra"],
};

/** STUB until MSAL claims exist. */
export function useCurrentUser(): CurrentUser {
  return STUB_USER;
}
