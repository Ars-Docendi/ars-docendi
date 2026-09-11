import type { PeriodoDesignacion } from "../types";

export type FiltroEstadoPeriodo = "activo" | "inactivo";

export interface FiltrosPeriodos {
  nombre: string;
  cargaDesde: string;
  cargaHasta: string;
  impactoDesde: string;
  impactoHasta: string;
  activo: FiltroEstadoPeriodo[];
}

export const FILTROS_PERIODOS_INICIALES: FiltrosPeriodos = {
  nombre: "",
  cargaDesde: "",
  cargaHasta: "",
  impactoDesde: "",
  impactoHasta: "",
  activo: [],
};

export type ColumnaOrdenPeriodos = keyof Omit<FiltrosPeriodos, "activo"> | "activo";

export interface OrdenPeriodos {
  columna: ColumnaOrdenPeriodos;
  direccion: "asc" | "desc";
}

const MESES = [
  "Enero",
  "Febrero",
  "Marzo",
  "Abril",
  "Mayo",
  "Junio",
  "Julio",
  "Agosto",
  "Septiembre",
  "Octubre",
  "Noviembre",
  "Diciembre",
];

export function normalizarTexto(texto: string): string {
  return texto
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "");
}

export function formatearFecha(fechaIso: string): string {
  const [anio, mes, dia] = fechaIso.split("-");
  return `${dia}/${mes}/${anio}`;
}

export function formatearMesAnio(fechaIso: string): string {
  const [anio, mes] = fechaIso.split("-");
  return `${MESES[Number(mes) - 1]} ${anio}`;
}

function textoFecha(fechaIso: string, mesAnio: boolean): string {
  return `${fechaIso} ${mesAnio ? formatearMesAnio(fechaIso) : formatearFecha(fechaIso)}`;
}

export function aplicarFiltrosPeriodos(
  periodos: PeriodoDesignacion[],
  filtros: FiltrosPeriodos,
): PeriodoDesignacion[] {
  const textos = {
    nombre: normalizarTexto(filtros.nombre),
    cargaDesde: normalizarTexto(filtros.cargaDesde),
    cargaHasta: normalizarTexto(filtros.cargaHasta),
    impactoDesde: normalizarTexto(filtros.impactoDesde),
    impactoHasta: normalizarTexto(filtros.impactoHasta),
  };
  return periodos.filter((periodo) => {
    if (textos.nombre && !normalizarTexto(periodo.nombre).includes(textos.nombre)) return false;
    if (
      textos.cargaDesde &&
      !normalizarTexto(textoFecha(periodo.cargaDesde, false)).includes(textos.cargaDesde)
    )
      return false;
    if (
      textos.cargaHasta &&
      !normalizarTexto(textoFecha(periodo.cargaHasta, false)).includes(textos.cargaHasta)
    )
      return false;
    if (
      textos.impactoDesde &&
      !normalizarTexto(textoFecha(periodo.impactoDesde, true)).includes(textos.impactoDesde)
    )
      return false;
    if (
      textos.impactoHasta &&
      !normalizarTexto(textoFecha(periodo.impactoHasta, true)).includes(textos.impactoHasta)
    )
      return false;
    if (filtros.activo.length) {
      const estado = periodo.activo ? "activo" : "inactivo";
      if (!filtros.activo.includes(estado)) return false;
    }
    return true;
  });
}

function compararTexto(a: string, b: string): number {
  const normalizadoA = normalizarTexto(a);
  const normalizadoB = normalizarTexto(b);
  if (!normalizadoA || !normalizadoB) return normalizadoA ? -1 : normalizadoB ? 1 : 0;
  return normalizadoA.localeCompare(normalizadoB, "es", { numeric: true });
}

function compararFecha(a: string, b: string): number {
  if (!a || !b) return a ? -1 : b ? 1 : 0;
  return Date.parse(a) - Date.parse(b);
}

function compararPeriodos(
  a: PeriodoDesignacion,
  b: PeriodoDesignacion,
  columna: ColumnaOrdenPeriodos,
): number {
  switch (columna) {
    case "nombre":
      return compararTexto(a.nombre, b.nombre);
    case "cargaDesde":
      return compararFecha(a.cargaDesde, b.cargaDesde);
    case "cargaHasta":
      return compararFecha(a.cargaHasta, b.cargaHasta);
    case "impactoDesde":
      return compararFecha(a.impactoDesde, b.impactoDesde);
    case "impactoHasta":
      return compararFecha(a.impactoHasta, b.impactoHasta);
    case "activo":
      return compararTexto(a.activo ? "Activo" : "Inactivo", b.activo ? "Activo" : "Inactivo");
  }
}

function valorPeriodo(periodo: PeriodoDesignacion, columna: ColumnaOrdenPeriodos): string {
  switch (columna) {
    case "nombre":
      return periodo.nombre;
    case "cargaDesde":
      return periodo.cargaDesde;
    case "cargaHasta":
      return periodo.cargaHasta;
    case "impactoDesde":
      return periodo.impactoDesde;
    case "impactoHasta":
      return periodo.impactoHasta;
    case "activo":
      return periodo.activo ? "Activo" : "Inactivo";
  }
}

export function ordenarPeriodos(
  periodos: PeriodoDesignacion[],
  orden: OrdenPeriodos | null,
): PeriodoDesignacion[] {
  const criterio = orden ?? { columna: "impactoDesde" as const, direccion: "desc" as const };
  const signo = criterio.direccion === "asc" ? 1 : -1;
  return [...periodos].sort((a, b) => {
    const valorA = valorPeriodo(a, criterio.columna).trim();
    const valorB = valorPeriodo(b, criterio.columna).trim();
    if (!valorA || !valorB) return valorA ? -1 : valorB ? 1 : 0;
    return compararPeriodos(a, b, criterio.columna) * signo || a.id.localeCompare(b.id);
  });
}

export function aplicarFiltrosYOrdenPeriodos(
  periodos: PeriodoDesignacion[],
  filtros: FiltrosPeriodos,
  orden: OrdenPeriodos | null,
): PeriodoDesignacion[] {
  return ordenarPeriodos(aplicarFiltrosPeriodos(periodos, filtros), orden);
}

export function siguienteOrdenPeriodos(
  orden: OrdenPeriodos | null,
  columna: ColumnaOrdenPeriodos,
): OrdenPeriodos | null {
  if (!orden || orden.columna !== columna) return { columna, direccion: "asc" };
  if (orden.direccion === "asc") return { columna, direccion: "desc" };
  return null;
}
