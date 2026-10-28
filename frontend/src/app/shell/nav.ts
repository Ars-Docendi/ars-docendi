import type { NavIconKey } from "./icons";

export interface NavItem {
  to: string;
  icon: NavIconKey;
  label: string;
  /** Con uno de los permisos alcanza. */
  permiso: string | string[];
  children?: NavItem[];
}

export interface NavGroup {
  label: string;
  items: NavItem[];
}

export const NAVEGACION: NavGroup[] = [
  {
    label: "Personal",
    items: [{ to: "/portal", icon: "portal", label: "Mi portal", permiso: "portal.ver" }],
  },
  {
    label: "Trabajo",
    items: [
      { to: "/aulas", icon: "aulas", label: "Reserva de aulas", permiso: "aulas.ver" },
      { to: "/tareas", icon: "tareas", label: "Tareas", permiso: "tareas.ver" },
      {
        to: "/tareas/proyectos",
        icon: "proyectos",
        label: "Proyectos",
        permiso: "tareas.gestionar",
      },
    ],
  },
  {
    label: "Designaciones",
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
    label: "Sistema",
    items: [
      {
        to: "/sistema",
        icon: "settings",
        label: "Dashboard del sistema",
        permiso: "sistema.estado.ver",
      },
      {
        to: "/auditoria",
        icon: "reportes",
        label: "Registros de auditoría",
        permiso: "auditoria.ver",
      },
    ],
  },
  {
    label: "Configuración",
    items: [
      {
        to: "/personas",
        icon: "usuarios",
        label: "Personas",
        permiso: ["personas.ver", "personas.ver_ambito"],
      },
      { to: "/roles", icon: "roles", label: "Roles", permiso: "roles.ver" },
    ],
  },
];

export function filtrarNavegacion(permisos: readonly string[]): NavGroup[] {
  const efectivos = new Set(permisos);
  return NAVEGACION.map((grupo) => ({
    ...grupo,
    items: grupo.items.filter((item) =>
      (Array.isArray(item.permiso) ? item.permiso : [item.permiso]).some((p) => efectivos.has(p)),
    ),
  })).filter((grupo) => grupo.items.length > 0);
}
