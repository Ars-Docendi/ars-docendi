import { ETIQUETAS_SCOPE, normalizarTexto, type RolMock, type ScopeRol } from "./models";

export type TipoRol = "sistema" | "personalizado";
export type ColumnaOrdenRol = "nombre" | "ambito" | "tipo" | "permisos" | "estado";
export type EstadoRol = "activo" | "inactivo";

export const ETIQUETAS_TIPO: Record<TipoRol, string> = {
  sistema: "Sistema",
  personalizado: "Personalizado",
};

export const ETIQUETAS_ESTADO: Record<EstadoRol, string> = {
  activo: "Activo",
  inactivo: "Inactivo",
};

export interface FiltrosRoles {
  nombre: string;
  descripcion: string;
  ambito: ScopeRol[];
  tipo: TipoRol[];
  estado: EstadoRol[];
}

export interface OrdenRoles {
  columna: ColumnaOrdenRol;
  direccion: "asc" | "desc";
}

export const FILTROS_VACIOS: FiltrosRoles = {
  nombre: "",
  descripcion: "",
  ambito: [],
  tipo: [],
  estado: [],
};

const tipoDe = (rol: RolMock): TipoRol => (rol.es_sistema ? "sistema" : "personalizado");
export const estadoDe = (rol: RolMock): EstadoRol => (rol.activo ? "activo" : "inactivo");
const contiene = (texto: string, filtro: string) =>
  !filtro.trim() || normalizarTexto(texto).includes(normalizarTexto(filtro.trim()));

export function filtrarRoles(roles: RolMock[], f: FiltrosRoles): RolMock[] {
  return roles.filter(
    (rol) =>
      contiene(rol.nombre, f.nombre) &&
      contiene(rol.descripcion, f.descripcion) &&
      (f.ambito.length === 0 || f.ambito.includes(rol.scope)) &&
      (f.tipo.length === 0 || f.tipo.includes(tipoDe(rol))) &&
      (f.estado.length === 0 || f.estado.includes(estadoDe(rol))),
  );
}

const comparar: Record<ColumnaOrdenRol, (a: RolMock, b: RolMock) => number> = {
  nombre: (a, b) => a.nombre.localeCompare(b.nombre, "es"),
  ambito: (a, b) => ETIQUETAS_SCOPE[a.scope].localeCompare(ETIQUETAS_SCOPE[b.scope], "es"),
  tipo: (a, b) => ETIQUETAS_TIPO[tipoDe(a)].localeCompare(ETIQUETAS_TIPO[tipoDe(b)], "es"),
  permisos: (a, b) => a.permisos.length - b.permisos.length,
  estado: (a, b) =>
    ETIQUETAS_ESTADO[estadoDe(a)].localeCompare(ETIQUETAS_ESTADO[estadoDe(b)], "es"),
};

export function ordenarRoles(roles: RolMock[], orden: OrdenRoles | null): RolMock[] {
  const o = orden ?? { columna: "nombre", direccion: "asc" };
  return [...roles].sort((a, b) => {
    const r = comparar[o.columna](a, b);
    return o.direccion === "desc" ? -r : r;
  });
}

/** Ciclo de las grillas del sistema: ascendente, descendente, sin orden. */
export function siguienteOrden(
  actual: OrdenRoles | null,
  columna: ColumnaOrdenRol,
): OrdenRoles | null {
  if (!actual || actual.columna !== columna) return { columna, direccion: "asc" };
  return actual.direccion === "asc" ? { columna, direccion: "desc" } : null;
}
