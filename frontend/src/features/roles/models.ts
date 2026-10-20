export interface PermisoRol {
  id: string;
  codigo: string;
  nombre: string;
  descripcion: string;
}

export interface RolMock {
  id: string;
  codigo: string;
  nombre: string;
  descripcion: string;
  scope: ScopeRol;
  es_sistema: boolean;
  activo: boolean;
  version: number;
  permisos: PermisoRol[];
}

export const SCOPES_ROL = ["global", "materia", "carrera"] as const;
export type ScopeRol = (typeof SCOPES_ROL)[number];
export const ETIQUETAS_SCOPE: Record<ScopeRol, string> = {
  global: "Global",
  materia: "Materia",
  carrera: "Carrera",
};
export type DatosRolNuevo = Pick<RolMock, "nombre" | "descripcion" | "scope">;
export type DatosRolEditables = DatosRolNuevo;
/** Misma equivalencia que lower(btrim(name)) de PostgreSQL; las tildes son significativas. */
export function normalizarNombreRol(nombre: string): string {
  return nombre.trim().toLowerCase();
}
export function normalizarTexto(s: string): string {
  return s.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
}
