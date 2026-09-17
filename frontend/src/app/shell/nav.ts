import type { NavIconKey } from "./icons";

export interface NavItem {
  to: string;
  icon: NavIconKey;
  label: string;
  permiso: string;
  children?: NavItem[];
}

export interface NavGroup {
  label: string;
  items: NavItem[];
}

export const NAVEGACION: NavGroup[] = [
  {
    label: "Personal",
    items: [{ to: "/portal", icon: "portal", label: "Mi Portal", permiso: "portal.ver" }],
  },
  {
    label: "Trabajo",
    items: [
      { to: "/aulas", icon: "aulas", label: "Reserva de aulas", permiso: "aulas.ver" },
      { to: "/tareas", icon: "tareas", label: "Tareas", permiso: "tareas.ver" },
    ],
  },
  {
    label: "DESIGNACIONES",
    items: [
      {
        to: "/designaciones/mis-pedidos",
        icon: "pedidos",
        label: "Mis pedidos",
        permiso: "designaciones.gestionar",
      },
      {
        to: "/designaciones/revision",
        icon: "revision",
        label: "Revisión",
        permiso: "designaciones.revisar",
      },
      {
        to: "/designaciones/periodos",
        icon: "periodos",
        label: "Períodos",
        permiso: "periodos.administrar",
      },
    ],
  },
  {
    label: "Configuración",
    items: [
      { to: "/usuarios", icon: "usuarios", label: "Usuarios", permiso: "usuarios.ver" },
      { to: "/docentes", icon: "docentes", label: "Docentes", permiso: "docentes.ver" },
      { to: "/roles", icon: "roles", label: "Roles", permiso: "roles.ver" },
    ],
  },
];

export function filtrarNavegacion(permisos: readonly string[]): NavGroup[] {
  const efectivos = new Set(permisos);
  return NAVEGACION.map((grupo) => ({
    ...grupo,
    items: grupo.items.filter((item) => efectivos.has(item.permiso)),
  })).filter((grupo) => grupo.items.length > 0);
}
