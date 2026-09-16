import type { UsuarioMock } from "./models";
import { nombreCompleto, normalizarTexto } from "./models";

export const VALOR_SIN_DATO = "__sin_dato__";
export type DireccionOrden = "asc" | "desc";
export type ColumnaOrdenUsuarios = "nombre" | "documento" | "legajo" | "upn" | "estado";

export interface OrdenUsuarios {
  columna: ColumnaOrdenUsuarios;
  direccion: DireccionOrden;
}

export interface FiltrosUsuarios {
  apellidoNombre: string;
  documento: string;
  legajo: string;
  upn: string;
  roles: string[];
  perfilDocente: ("si" | "no")[];
  estado: ("activo" | "inactivo")[];
}

export const FILTROS_USUARIOS_VACIOS: FiltrosUsuarios = {
  apellidoNombre: "",
  documento: "",
  legajo: "",
  upn: "",
  roles: [],
  perfilDocente: [],
  estado: [],
};

export function aplicarFiltrosUsuarios(
  usuarios: UsuarioMock[],
  filtros: FiltrosUsuarios,
): UsuarioMock[] {
  const nombreBuscado = normalizarTexto(filtros.apellidoNombre);
  const documentoBuscado = normalizarTexto(filtros.documento);
  const legajoBuscado = normalizarTexto(filtros.legajo);
  const upnBuscado = normalizarTexto(filtros.upn);

  return usuarios.filter((usuario) => {
    if (
      nombreBuscado &&
      ![
        usuario.apellido,
        usuario.nombre,
        nombreCompleto(usuario),
        `${usuario.nombre} ${usuario.apellido}`,
      ].some((valor) => normalizarTexto(valor).includes(nombreBuscado))
    )
      return false;
    if (documentoBuscado && !normalizarTexto(usuario.documento).includes(documentoBuscado))
      return false;
    if (legajoBuscado && !normalizarTexto(usuario.legajo).includes(legajoBuscado)) return false;
    if (upnBuscado && !normalizarTexto(usuario.upn).includes(upnBuscado)) return false;
    if (filtros.roles.length && !filtros.roles.some((rol) => usuario.roles.includes(rol))) {
      if (!(filtros.roles.includes(VALOR_SIN_DATO) && usuario.roles.length === 0)) return false;
    }
    if (
      filtros.perfilDocente.length &&
      !filtros.perfilDocente.includes(usuario.perfilDocente.esDocente ? "si" : "no")
    )
      return false;
    if (
      filtros.estado.length &&
      !filtros.estado.includes(usuario.is_active ? "activo" : "inactivo")
    )
      return false;
    return true;
  });
}

export function ordenarUsuarios(
  usuarios: UsuarioMock[],
  orden: OrdenUsuarios | null,
): UsuarioMock[] {
  if (!orden) return [...usuarios];
  const signo = orden.direccion === "asc" ? 1 : -1;
  return [...usuarios].sort((a, b) => {
    const comparacion = compararUsuariosConOrden(a, b, orden.columna, signo);
    return comparacion || a.id.localeCompare(b.id) * signo;
  });
}

export function aplicarFiltrosYOrdenUsuarios(
  usuarios: UsuarioMock[],
  filtros: FiltrosUsuarios,
  orden: OrdenUsuarios | null,
): UsuarioMock[] {
  const filtrados = aplicarFiltrosUsuarios(usuarios, filtros);
  return ordenarUsuarios(filtrados, orden);
}

export function siguienteOrdenUsuarios(
  orden: OrdenUsuarios | null,
  columna: ColumnaOrdenUsuarios,
): OrdenUsuarios | null {
  if (!orden || orden.columna !== columna) return { columna, direccion: "asc" };
  if (orden.direccion === "asc") return { columna, direccion: "desc" };
  return null;
}

export function opcionesRolesUsuarios(usuarios: UsuarioMock[]): string[] {
  const roles = new Set(usuarios.flatMap((usuario) => usuario.roles));
  if (usuarios.some((usuario) => usuario.roles.length === 0)) roles.add(VALOR_SIN_DATO);
  return [...roles].sort(compararTexto);
}

export function compararUsuarios(
  a: UsuarioMock,
  b: UsuarioMock,
  columna: ColumnaOrdenUsuarios,
): number {
  switch (columna) {
    case "nombre":
      return compararTexto(`${a.apellido} ${a.nombre}`, `${b.apellido} ${b.nombre}`);
    case "documento":
      return compararTexto(a.documento, b.documento);
    case "legajo":
      return compararLegajos(a.legajo, b.legajo);
    case "upn":
      return compararTexto(a.upn, b.upn);
    case "estado":
      return compararTexto(
        a.is_active ? "Activo" : "Inactivo",
        b.is_active ? "Activo" : "Inactivo",
      );
  }
}

function compararUsuariosConOrden(
  a: UsuarioMock,
  b: UsuarioMock,
  columna: ColumnaOrdenUsuarios,
  signo: number,
): number {
  const valorA = valorOrdenUsuarios(a, columna).trim();
  const valorB = valorOrdenUsuarios(b, columna).trim();
  if (!valorA || !valorB) return valorA ? -1 : valorB ? 1 : 0;
  return compararUsuarios(a, b, columna) * signo;
}

function valorOrdenUsuarios(usuario: UsuarioMock, columna: ColumnaOrdenUsuarios): string {
  switch (columna) {
    case "nombre":
      return `${usuario.apellido} ${usuario.nombre}`;
    case "documento":
      return usuario.documento;
    case "legajo":
      return usuario.legajo;
    case "upn":
      return usuario.upn;
    case "estado":
      return usuario.is_active ? "Activo" : "Inactivo";
  }
}

function compararLegajos(a: string, b: string): number {
  const aNormalizado = a.trim();
  const bNormalizado = b.trim();
  if (!aNormalizado || !bNormalizado) return aNormalizado ? -1 : bNormalizado ? 1 : 0;
  if (/^\d+$/.test(aNormalizado) && /^\d+$/.test(bNormalizado)) {
    return Number(aNormalizado) - Number(bNormalizado);
  }
  return compararTexto(aNormalizado, bNormalizado);
}

function compararTexto(a: string, b: string): number {
  const aNormalizado = normalizarTexto(a).trim();
  const bNormalizado = normalizarTexto(b).trim();
  if (!aNormalizado || !bNormalizado) return aNormalizado ? -1 : bNormalizado ? 1 : 0;
  return aNormalizado.localeCompare(bNormalizado, "es", { numeric: true });
}
