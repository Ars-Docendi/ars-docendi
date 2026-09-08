// ============================================================
// Role-aware sidebar navigation config.
// Ported from the design's NAV_BY_ROLE, but every item points at
// a REAL route (the module routes). Design items that target
// not-yet-built screens (Reportes, Configuración) are omitted
// rather than rendered as dead links (invariant #7: no fake UI).
// Inbox count badges are intentionally absent until a backend can
// supply real numbers.
//
// Las pantallas del circuito viven en el sector DESIGNACIONES. No se agrega
// un enlace padre: cada pantalla autorizada es un enlace directo.
// ============================================================
import type { Role } from "../../shared/auth/useCurrentUser";
import type { NavIconKey } from "./icons";

export interface NavItem {
  /** Absolute route path; must resolve to a working route. */
  to: string;
  icon: NavIconKey;
  label: string;
  /** Sub-rutas anidadas que cuelgan de este ítem en un grupo colapsable. */
  children?: NavItem[];
}

export interface NavGroup {
  label: string;
  items: NavItem[];
}

export const NAV_BY_ROLE: Record<Role, NavGroup[]> = {
  "Jefe de Cátedra": [
    {
      label: "Trabajo",
      items: [{ to: "/aulas", icon: "aulas", label: "Reserva de aulas" }],
    },
    {
      label: "DESIGNACIONES",
      items: [{ to: "/designaciones/mis-pedidos", icon: "pedidos", label: "Mis pedidos" }],
    },
    {
      label: "Configuración",
      items: [{ to: "/docentes", icon: "docentes", label: "Mis Docentes" }],
    },
    {
      label: "Personal",
      items: [{ to: "/portal", icon: "portal", label: "Mi Portal" }],
    },
  ],
  Coordinador: [
    {
      label: "Trabajo",
      items: [{ to: "/tareas", icon: "tareas", label: "Tareas" }],
    },
    {
      label: "DESIGNACIONES",
      items: [{ to: "/designaciones/revision", icon: "revision", label: "Revisión" }],
    },
    {
      label: "Personal",
      items: [{ to: "/portal", icon: "portal", label: "Mi Portal" }],
    },
  ],
  Secretaría: [
    {
      label: "Trabajo",
      items: [
        { to: "/aulas", icon: "aulas", label: "Reserva de aulas" },
        { to: "/tareas", icon: "tareas", label: "Tareas" },
      ],
    },
    {
      label: "DESIGNACIONES",
      items: [
        { to: "/designaciones/revision", icon: "revision", label: "Revisión" },
        { to: "/designaciones/periodos", icon: "periodos", label: "Períodos" },
      ],
    },
    {
      label: "Configuración",
      items: [
        { to: "/usuarios", icon: "usuarios", label: "Usuarios" },
        { to: "/docentes", icon: "docentes", label: "Docentes" },
        { to: "/roles", icon: "roles", label: "Roles" },
        { to: "/membresia-roles", icon: "membresiaRoles", label: "Membresía Roles" },
      ],
    },
    {
      label: "Personal",
      items: [{ to: "/portal", icon: "portal", label: "Mi Portal" }],
    },
  ],
  Decanato: [
    {
      label: "Trabajo",
      items: [{ to: "/tareas", icon: "tareas", label: "Tareas" }],
    },
    {
      label: "DESIGNACIONES",
      items: [{ to: "/designaciones/revision", icon: "revision", label: "Revisión" }],
    },
    {
      label: "Personal",
      items: [{ to: "/portal", icon: "portal", label: "Mi Portal" }],
    },
  ],
  Administración: [
    {
      label: "Trabajo",
      items: [
        { to: "/aulas", icon: "aulas", label: "Reserva de aulas" },
        { to: "/tareas", icon: "tareas", label: "Tareas" },
      ],
    },
    {
      label: "DESIGNACIONES",
      items: [{ to: "/designaciones/revision", icon: "revision", label: "Revisión" }],
    },
    {
      label: "Configuración",
      items: [
        { to: "/usuarios", icon: "usuarios", label: "Usuarios" },
        { to: "/docentes", icon: "docentes", label: "Docentes" },
        { to: "/roles", icon: "roles", label: "Roles" },
        { to: "/membresia-roles", icon: "membresiaRoles", label: "Membresía Roles" },
      ],
    },
    {
      label: "Personal",
      items: [{ to: "/portal", icon: "portal", label: "Mi Portal" }],
    },
  ],
  Docente: [
    {
      label: "Trabajo",
      items: [{ to: "/aulas", icon: "aulas", label: "Reserva de aulas" }],
    },
    {
      label: "Personal",
      items: [{ to: "/portal", icon: "portal", label: "Mi Portal" }],
    },
  ],
};
