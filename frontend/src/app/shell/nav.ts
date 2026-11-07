import type { NavIconKey } from "./icons";

export interface NavItem {
  to: string;
  icon: NavIconKey;
  label: string;
  /**
   * Un permiso único, o una lista: con una lista alcanza con CUALQUIERA de
   * los permisos indicados (any-of), como en `RequirePermission`.
   */
  permiso: string | readonly string[];
  /**
   * Permisos que el ítem exige TODOS, además de `permiso`: para una pantalla
   * que necesita dos permisos a la vez para ser usable.
   */
  ademas?: readonly string[];
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
      {
        to: "/aulas",
        icon: "aulas",
        label: "Reserva de aulas",
        // Los mismos permisos que exige la ruta /aulas: con sólo aulas.ver la pantalla
        // redirige, así que la entrada no se muestra.
        permiso: ["aulas.solicitar", "aulas.aprobar"],
      },
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
        to: "/designaciones/conciliacion",
        icon: "conciliacion",
        label: "Conciliación",
        permiso: "designaciones.conciliar",
      },
      {
        to: "/designaciones/periodos",
        icon: "periodos",
        label: "Períodos",
        permiso: "periodos.administrar",
      },
      {
        to: "/designaciones/comisiones",
        icon: "comisiones",
        label: "Comisiones",
        permiso: "comisiones.ver",
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
      {
        to: "/personas",
        icon: "usuarios",
        label: "Personas",
        permiso: ["personas.ver", "personas.ver_ambito"],
      },
      { to: "/roles", icon: "roles", label: "Roles", permiso: "roles.ver" },
      {
        to: "/asistente/soporte-historial",
        icon: "historial",
        label: "Historial del asistente",
        // Sembrado a NINGÚN rol por default (asistente-acceso-de-soporte-al-historial):
        // sin el permiso, este ítem no aparece, y sin el ítem no hay cómo llegar a la
        // ruta desde la navegación —la ruta en sí también la protege
        // `RequirePermission` (ver features/asistente/routes.tsx), así que escribir la
        // URL a mano tampoco alcanza. `personas.ver` también hace falta: el
        // selector de sujeto lee el listado de usuarios, que el backend protege
        // con ese permiso.
        permiso: "asistente.leer_historial_ajeno",
        ademas: ["personas.ver"],
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
    items: grupo.items.filter(
      (item) =>
        tienePermiso(efectivos, item.permiso) && (item.ademas ?? []).every((p) => efectivos.has(p)),
    ),
  })).filter((grupo) => grupo.items.length > 0);
}
