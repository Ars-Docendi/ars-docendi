import type { Persona } from "./api/personasApi";

export const SIN_ROL = "__sin_rol__";
export type ColumnaOrden = "nombre" | "documento" | "legajo" | "estado";
export type EstadoPersona = "activo" | "inactivo" | "sin_cuenta";

export interface FiltrosPersonas {
  nombre: string;
  documento: string;
  legajo: string;
  asignaciones: string;
  rol: string[];
  estado: EstadoPersona[];
}

export interface OrdenPersonas {
  columna: ColumnaOrden;
  direccion: "asc" | "desc";
}

export const FILTROS_VACIOS: FiltrosPersonas = {
  nombre: "",
  documento: "",
  legajo: "",
  asignaciones: "",
  rol: [],
  estado: [],
};

export const nombreCompleto = (p: Pick<Persona, "apellido" | "nombre">) =>
  `${p.apellido}, ${p.nombre}`;

export function estadoDe(p: Persona): EstadoPersona {
  if (!p.tieneCuenta) return "sin_cuenta";
  return p.activo ? "activo" : "inactivo";
}

function normalizar(texto: string | null | undefined) {
  return (texto ?? "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().trim();
}

const contiene = (texto: string | null | undefined, filtro: string) =>
  !filtro.trim() || normalizar(texto).includes(normalizar(filtro));

const textoAsignaciones = (p: Persona) =>
  p.asignaciones
    .map((a) => `${a.materiaCodigo} ${a.materiaNombre} ${a.cargoNombre} ${a.cargoAbreviatura}`)
    .join(" ");

export function opcionesRol(personas: Persona[]): string[] {
  const nombres = [...new Set(personas.flatMap((p) => p.roles.map((r) => r.nombre)))].sort((a, b) =>
    a.localeCompare(b),
  );
  return personas.some((p) => p.roles.length === 0) ? [...nombres, SIN_ROL] : nombres;
}

export function filtrarPersonas(personas: Persona[], f: FiltrosPersonas): Persona[] {
  return personas.filter((p) => {
    const roles = p.roles.map((r) => r.nombre);
    return (
      contiene(nombreCompleto(p), f.nombre) &&
      contiene(p.documento, f.documento) &&
      contiene(p.legajo, f.legajo) &&
      contiene(textoAsignaciones(p), f.asignaciones) &&
      (f.rol.length === 0 ||
        f.rol.some((r) => (r === SIN_ROL ? roles.length === 0 : roles.includes(r)))) &&
      (f.estado.length === 0 || f.estado.includes(estadoDe(p)))
    );
  });
}

const valorOrden: Record<ColumnaOrden, (p: Persona) => string> = {
  nombre: nombreCompleto,
  documento: (p) => p.documento,
  legajo: (p) => p.legajo ?? "",
  estado: estadoDe,
};

export function ordenarPersonas(personas: Persona[], orden: OrdenPersonas | null): Persona[] {
  const o = orden ?? { columna: "nombre", direccion: "asc" };
  return [...personas].sort((a, b) => {
    const r = valorOrden[o.columna](a).localeCompare(valorOrden[o.columna](b), "es", {
      numeric: true,
    });
    return o.direccion === "desc" ? -r : r;
  });
}

/** Ciclo de las grillas del sistema: ascendente, descendente, sin orden. */
export function siguienteOrden(
  actual: OrdenPersonas | null,
  columna: ColumnaOrden,
): OrdenPersonas | null {
  if (!actual || actual.columna !== columna) return { columna, direccion: "asc" };
  return actual.direccion === "asc" ? { columna, direccion: "desc" } : null;
}
