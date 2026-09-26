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
      { to: "/usuarios", icon: "usuarios", label: "Usuarios", permiso: "usuarios.ver" },
      { to: "/docentes", icon: "docentes", label: "Docentes", permiso: "docentes.ver" },
      { to: "/roles", icon: "roles", label: "Roles", permiso: "roles.ver" },
      {
        to: "/asistente/soporte-historial",
        icon: "historial",
        label: "Historial del asistente",
        // Sembrado a NINGÚN rol por default (asistente-acceso-de-soporte-al-historial):
        // sin el permiso, este ítem no aparece, y sin el ítem no hay cómo llegar a la
        // ruta desde la navegación —la ruta en sí también la protege
        // `RequirePermission` (ver features/asistente/routes.tsx), así que escribir la
        // URL a mano tampoco alcanza.
        permiso: "asistente.leer_historial_ajeno",
      },
      {
        to: "/asistente/administracion",
        icon: "reportes",
        label: "Uso del asistente",
        // Sembrado directamente a `sys_admin` (asistente-administracion-de-uso,
        // design.md D13): mismo criterio que el ítem de arriba —sin el permiso
        // no hay ítem, y la ruta misma también la protege `RequirePermission`.
        permiso: "asistente.administrar",
      },
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
