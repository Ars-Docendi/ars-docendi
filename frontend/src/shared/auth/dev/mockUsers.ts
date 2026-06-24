// ============================================================
// DEV MOCK LOGIN — remove with shared/auth/dev/
// ------------------------------------------------------------
// Hardcoded test users, one per UI-functional role, used by the
// mock login modal until Azure AD SSO is wired. Fully client-side:
// NO backend calls. Shapes mirror the DB so the mock data stays
// faithful to dev/database/identity/ (users + roles + user_roles):
//   - `db`         → identity.users row
//   - `assignment` → identity.user_roles + identity.roles (joined)
//   - `currentUser`→ the view the app shell actually consumes
//                    (drives NAV_BY_ROLE + the RoleBadge).
//
// sys_admin (role_id 7) is intentionally omitted: it has no nav/UI
// surface, so it would render an empty shell.
//
// To delete this whole feature: remove this `dev/` folder and revert
// the two blocks marked `DEV MOCK LOGIN` in LoginPage.tsx and
// useCurrentUser.ts.
// ============================================================
import type { CurrentUser } from "../useCurrentUser";

/** Role scope as defined by identity.roles.scope CHECK constraint. */
type Scope = "global" | "materia" | "carrera";

/** identity.user_roles row joined with identity.roles (code/name/scope). */
interface MockRoleAssignment {
  role_id: number;
  code: string;
  name: string;
  scope: Scope;
  /** Set when scope is `carrera` or `materia` (materia ⇒ carrera). */
  carrera_id: string | null;
  /** Set only when scope is `materia`. */
  materia_id: string | null;
}

/** identity.users row. */
interface MockUserDbRow {
  id: string;
  azure_oid: string;
  upn: string;
  display_name: string;
  is_active: boolean;
}

export interface MockUser {
  /** Selector id — reuses the identity.users UUID. */
  id: string;
  db: MockUserDbRow;
  assignment: MockRoleAssignment;
  /** Derived shape the app shell renders. */
  currentUser: CurrentUser;
}

// Shared scope references so the materia ⇒ carrera invariant is obvious.
const CARRERA_INFORMATICA = "c0000000-0000-4000-8000-000000000201";
const MATERIA_ING_SOFTWARE = "70000000-0000-4000-8000-000000000101";
const MATERIA_ALGORITMOS = "70000000-0000-4000-8000-000000000102";

export const MOCK_USERS: MockUser[] = [
  {
    id: "a0000000-0000-4000-8000-000000000001",
    db: {
      id: "a0000000-0000-4000-8000-000000000001",
      azure_oid: "00000000-0000-4000-8000-000000000001",
      upn: "carla.lopez@unlam.edu.ar",
      display_name: "Carla López",
      is_active: true,
    },
    assignment: {
      role_id: 1,
      code: "docente",
      name: "Docente",
      scope: "materia",
      carrera_id: CARRERA_INFORMATICA,
      materia_id: MATERIA_ALGORITMOS,
    },
    currentUser: {
      name: "C. López",
      initials: "CL",
      upn: "carla.lopez@unlam.edu.ar",
      role: "Docente",
      roles: ["Docente"],
    },
  },
  {
    id: "a0000000-0000-4000-8000-000000000002",
    db: {
      id: "a0000000-0000-4000-8000-000000000002",
      azure_oid: "00000000-0000-4000-8000-000000000002",
      upn: "gustavo.ruiz@unlam.edu.ar",
      display_name: "Gustavo Ruiz",
      is_active: true,
    },
    assignment: {
      role_id: 2,
      code: "jefe_catedra",
      name: "Jefe de Cátedra",
      scope: "materia",
      carrera_id: CARRERA_INFORMATICA,
      materia_id: MATERIA_ING_SOFTWARE,
    },
    currentUser: {
      name: "G. Ruiz",
      initials: "GR",
      upn: "gustavo.ruiz@unlam.edu.ar",
      role: "Jefe de Cátedra",
      roles: ["Jefe de Cátedra"],
    },
  },
  {
    id: "a0000000-0000-4000-8000-000000000003",
    db: {
      id: "a0000000-0000-4000-8000-000000000003",
      azure_oid: "00000000-0000-4000-8000-000000000003",
      upn: "marina.diaz@unlam.edu.ar",
      display_name: "Marina Díaz",
      is_active: true,
    },
    assignment: {
      role_id: 3,
      code: "coordinador_carrera",
      name: "Coordinador de Carrera",
      scope: "carrera",
      carrera_id: CARRERA_INFORMATICA,
      materia_id: null,
    },
    currentUser: {
      name: "M. Díaz",
      initials: "MD",
      upn: "marina.diaz@unlam.edu.ar",
      role: "Coordinador",
      roles: ["Coordinador"],
    },
  },
  {
    id: "a0000000-0000-4000-8000-000000000004",
    db: {
      id: "a0000000-0000-4000-8000-000000000004",
      azure_oid: "00000000-0000-4000-8000-000000000004",
      upn: "secretaria.academica@unlam.edu.ar",
      display_name: "Lucía Fernández",
      is_active: true,
    },
    assignment: {
      role_id: 4,
      code: "secretaria",
      name: "Secretaría Académica",
      scope: "global",
      carrera_id: null,
      materia_id: null,
    },
    currentUser: {
      name: "L. Fernández",
      initials: "LF",
      upn: "secretaria.academica@unlam.edu.ar",
      role: "Secretaría",
      roles: ["Secretaría"],
    },
  },
  {
    id: "a0000000-0000-4000-8000-000000000005",
    db: {
      id: "a0000000-0000-4000-8000-000000000005",
      azure_oid: "00000000-0000-4000-8000-000000000005",
      upn: "decanato@unlam.edu.ar",
      display_name: "Roberto Sosa",
      is_active: true,
    },
    assignment: {
      role_id: 5,
      code: "decanato",
      name: "Decanato",
      scope: "global",
      carrera_id: null,
      materia_id: null,
    },
    currentUser: {
      name: "R. Sosa",
      initials: "RS",
      upn: "decanato@unlam.edu.ar",
      role: "Decanato",
      roles: ["Decanato"],
    },
  },
  {
    id: "a0000000-0000-4000-8000-000000000006",
    db: {
      id: "a0000000-0000-4000-8000-000000000006",
      azure_oid: "00000000-0000-4000-8000-000000000006",
      upn: "admin.aulas@unlam.edu.ar",
      display_name: "Paula Gómez",
      is_active: true,
    },
    assignment: {
      role_id: 6,
      code: "administrativo",
      name: "Administrativo",
      scope: "global",
      carrera_id: null,
      materia_id: null,
    },
    currentUser: {
      name: "P. Gómez",
      initials: "PG",
      upn: "admin.aulas@unlam.edu.ar",
      role: "Administración",
      roles: ["Administración"],
    },
  },
];
