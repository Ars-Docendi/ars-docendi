import type { NavIconKey } from "./icons";

export interface NavItem {
  to: string;
  icon: NavIconKey;
  label: string;
  /**
   * Un permiso único, o una lista: con una lista alcanza con CUALQUIERA de
   * los permisos indicados (any-of), como en `RequirePermission` (design D9,
   * sistema-seccion-unificada). El ítem «Sistema» es el primer caso real.
   */
  permiso: string | readonly string[];
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
        label: "Sistema",
        // Un solo ítem para las tres pestañas del ARS-154: alcanza con
        // cualquiera de sus tres permisos, no con los tres a la vez.
        permiso: ["sistema.estado.ver", "asistente.administrar", "auditoria.ver"],
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
      // «Uso del asistente» ya no es un ítem propio: sistema-seccion-unificada
      // (ARS-154) lo absorbe en la pestaña Asistente del ítem «Sistema» de
      // arriba (design D8/D9); `asistente.administrar` sigue gateando la
      // pestaña y cada endpoint, no un ítem de navegación separado.
    ],
  },
];

function tienePermiso(efectivos: Set<string>, permiso: string | readonly string[]): boolean {
  const requeridos = Array.isArray(permiso) ? permiso : [permiso];
  return requeridos.some((p) => efectivos.has(p));
}

export function filtrarNavegacion(permisos: readonly string[]): NavGroup[] {
  const efectivos = new Set(permisos);
  return NAVEGACION.map((grupo) => ({
    ...grupo,
    items: grupo.items.filter((item) => tienePermiso(efectivos, item.permiso)),
  })).filter((grupo) => grupo.items.length > 0);
}
