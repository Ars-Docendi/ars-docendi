import type { PermisoRol } from "./models";

/** Módulos en el orden en que se muestran; la clave es el prefijo del código del permiso. */
const MODULOS: Record<string, string> = {
  personas: "Personas",
  portal: "Portal",
  designaciones: "Designaciones",
  tareas: "Tareas",
  proyectos: "Proyectos",
  periodos: "Períodos",
  roles: "Roles",
  auditoria: "Auditoría",
  sistema: "Sistema",
};
const ORDEN = Object.keys(MODULOS);

export interface ModuloPermisos {
  clave: string;
  nombre: string;
  permisos: PermisoRol[];
}

const prefijo = (codigo: string) => codigo.split(".")[0];

/**
 * Agrupa el catálogo por el prefijo del código. Un prefijo sin entrada en el mapa
 * se muestra al final con su prefijo como nombre, para que ningún permiso quede oculto.
 */
export function agruparPorModulo(permisos: PermisoRol[]): ModuloPermisos[] {
  const grupos = new Map<string, PermisoRol[]>();
  for (const permiso of permisos) {
    const clave = prefijo(permiso.codigo);
    grupos.set(clave, [...(grupos.get(clave) ?? []), permiso]);
  }
  const posicion = (clave: string) => (ORDEN.includes(clave) ? ORDEN.indexOf(clave) : ORDEN.length);
  return [...grupos.entries()]
    .sort(([a], [b]) => posicion(a) - posicion(b) || a.localeCompare(b))
    .map(([clave, lista]) => ({
      clave,
      nombre: MODULOS[clave] ?? clave.charAt(0).toUpperCase() + clave.slice(1),
      permisos: [...lista].sort((a, b) => a.nombre.localeCompare(b.nombre, "es")),
    }));
}
