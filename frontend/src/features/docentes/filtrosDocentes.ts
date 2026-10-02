import type { DocenteMock } from "./models";
import { nombreCompleto, normalizarTexto } from "./models";

export const VALOR_SIN_DATO = "__sin_dato__";
export type DireccionOrden = "asc" | "desc";
export type ColumnaOrdenDocentes = "nombre" | "documento" | "legajo" | "cuenta" | "estado";

export interface OrdenDocentes {
  columna: ColumnaOrdenDocentes;
  direccion: DireccionOrden;
}

export interface FiltrosDocentes {
  apellidoNombre: string;
  documento: string;
  legajo: string;
  rol: string[];
  ambitos: string[];
  asignaciones: string;
  cuenta: ("con_cuenta" | "sin_cuenta")[];
  estado: ("activo" | "inactivo")[];
}

export const FILTROS_DOCENTES_VACIOS: FiltrosDocentes = {
  apellidoNombre: "",
  documento: "",
  legajo: "",
  rol: [],
  ambitos: [],
  asignaciones: "",
  cuenta: [],
  estado: [],
};

export function aplicarFiltrosDocentes(
  docentes: DocenteMock[],
  filtros: FiltrosDocentes,
): DocenteMock[] {
  const nombreBuscado = normalizarTexto(filtros.apellidoNombre);
  const documentoBuscado = normalizarTexto(filtros.documento);
  const legajoBuscado = normalizarTexto(filtros.legajo);
  const asignacionBuscada = normalizarTexto(filtros.asignaciones);

  return docentes.filter((docente) => {
    if (
      nombreBuscado &&
      ![
        docente.apellido,
        docente.nombre,
        nombreCompleto(docente),
        `${docente.nombre} ${docente.apellido}`,
      ].some((valor) => normalizarTexto(valor).includes(nombreBuscado))
    )
      return false;
    if (documentoBuscado && !normalizarTexto(docente.documento).includes(documentoBuscado))
      return false;
    if (legajoBuscado && !normalizarTexto(docente.legajo).includes(legajoBuscado)) return false;
    if (
      asignacionBuscada &&
      !docente.asignaciones.some((asignacion) =>
        [
          asignacion.materia.codigo,
          asignacion.materia.nombre,
          asignacion.cargo,
          asignacion.cargoAbreviatura ?? "",
        ].some((valor) => normalizarTexto(valor).includes(asignacionBuscada)),
      )
    )
      return false;
    if (
      filtros.rol.length &&
      !filtros.rol.some((rol) => docente.roles.includes(rol)) &&
      !(filtros.rol.includes(VALOR_SIN_DATO) && docente.roles.length === 0)
    )
      return false;
    if (
      filtros.ambitos.length &&
      !filtros.ambitos.some((ambito) =>
        docente.membresias.some((membresia) => membresia.ambito === ambito),
      ) &&
      !(filtros.ambitos.includes(VALOR_SIN_DATO) && docente.membresias.length === 0)
    )
      return false;
    if (
      filtros.cuenta.length &&
      !filtros.cuenta.includes(docente.tieneCuenta ? "con_cuenta" : "sin_cuenta")
    )
      return false;
    if (
      filtros.estado.length &&
      !filtros.estado.includes(docente.is_active ? "activo" : "inactivo")
    )
      return false;
    return true;
  });
}

export function aplicarFiltrosYOrdenDocentes(
  docentes: DocenteMock[],
  filtros: FiltrosDocentes,
  orden: OrdenDocentes | null,
): DocenteMock[] {
  const filtrados = aplicarFiltrosDocentes(docentes, filtros);
  return ordenarDocentes(filtrados, orden);
}

export function ordenarDocentes(
  docentes: DocenteMock[],
  orden: OrdenDocentes | null,
): DocenteMock[] {
  if (!orden) return [...docentes];
  const signo = orden.direccion === "asc" ? 1 : -1;
  return [...docentes].sort((a, b) => {
    const comparacion = compararDocentesConOrden(a, b, orden.columna, signo);
    return comparacion || a.id.localeCompare(b.id) * signo;
  });
}

export function siguienteOrdenDocentes(
  orden: OrdenDocentes | null,
  columna: ColumnaOrdenDocentes,
): OrdenDocentes | null {
  if (!orden || orden.columna !== columna) return { columna, direccion: "asc" };
  if (orden.direccion === "asc") return { columna, direccion: "desc" };
  return null;
}

export function opcionesRolesDocentes(docentes: DocenteMock[]): string[] {
  const roles = new Set(docentes.flatMap((docente) => docente.roles));
  if (docentes.some((docente) => docente.roles.length === 0)) roles.add(VALOR_SIN_DATO);
  return [...roles].sort(compararTexto);
}

export function opcionesAmbitosDocentes(docentes: DocenteMock[]): string[] {
  const ambitos = new Set(
    docentes.flatMap((docente) => docente.membresias.map((membresia) => membresia.ambito)),
  );
  if (docentes.some((docente) => docente.membresias.length === 0)) ambitos.add(VALOR_SIN_DATO);
  return [...ambitos].sort(compararTexto);
}

export function compararDocentes(
  a: DocenteMock,
  b: DocenteMock,
  columna: ColumnaOrdenDocentes,
): number {
  switch (columna) {
    case "nombre":
      return compararTexto(`${a.apellido} ${a.nombre}`, `${b.apellido} ${b.nombre}`);
    case "documento":
      return compararTexto(a.documento, b.documento);
    case "legajo":
      return compararLegajos(a.legajo, b.legajo);
    case "cuenta":
      return compararTexto(
        a.tieneCuenta ? "Con cuenta" : "Sin cuenta",
        b.tieneCuenta ? "Con cuenta" : "Sin cuenta",
      );
    case "estado":
      return compararTexto(
        a.is_active ? "Activo" : "Inactivo",
        b.is_active ? "Activo" : "Inactivo",
      );
  }
}

function compararDocentesConOrden(
  a: DocenteMock,
  b: DocenteMock,
  columna: ColumnaOrdenDocentes,
  signo: number,
): number {
  const valorA = valorOrdenDocentes(a, columna).trim();
  const valorB = valorOrdenDocentes(b, columna).trim();
  if (!valorA || !valorB) return valorA ? -1 : valorB ? 1 : 0;
  return compararDocentes(a, b, columna) * signo;
}

function valorOrdenDocentes(docente: DocenteMock, columna: ColumnaOrdenDocentes): string {
  switch (columna) {
    case "nombre":
      return `${docente.apellido} ${docente.nombre}`;
    case "documento":
      return docente.documento;
    case "legajo":
      return docente.legajo;
    case "cuenta":
      return docente.tieneCuenta ? "Con cuenta" : "Sin cuenta";
    case "estado":
      return docente.is_active ? "Activo" : "Inactivo";
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
