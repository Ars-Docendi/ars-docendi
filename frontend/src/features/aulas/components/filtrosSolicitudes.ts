import type { EstadoSolicitudAula, SolicitudReservaAula } from "../types";

export interface FiltrosSolicitudesState {
  dia: string;
  materia: string;
  comision: string;
  docente: string;
  estado: EstadoSolicitudAula[];
}

export const FILTROS_INICIALES: FiltrosSolicitudesState = {
  dia: "",
  materia: "",
  comision: "",
  docente: "",
  estado: [],
};

export type ColumnaOrdenSolicitudes =
  "dia" | "horario" | "alumnos" | "materia" | "comision" | "estado" | "docente";

export interface OrdenSolicitudes {
  columna: ColumnaOrdenSolicitudes;
  direccion: "asc" | "desc";
}

const ESTADOS: EstadoSolicitudAula[] = ["pendiente", "aprobada", "rechazada", "cancelada"];

/** Minúsculas y sin diacríticos, para comparar sin distinguir mayúsculas/acentos. */
export function normalizarTexto(texto: string): string {
  return texto.trim().toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "");
}

/** Fecha en formato dd/mm/aaaa a partir de un `DateOnly` ISO ("aaaa-mm-dd"). */
export function fechaSolicitud(solicitud: SolicitudReservaAula): string {
  const [anio, mes, dia] = solicitud.dia.split("-");
  return `${dia}/${mes}/${anio}`;
}

/** "HH:mm" a "HH:mm" — recorta segundos si el backend los incluye. */
function horaCorta(valor: string): string {
  return valor.slice(0, 5);
}

export function horarioSolicitud(solicitud: SolicitudReservaAula): string {
  return `${horaCorta(solicitud.horarioDesde)} a ${horaCorta(solicitud.horarioHasta)}`;
}

export function etiquetaEstado(estado: EstadoSolicitudAula): string {
  return {
    pendiente: "Pendiente",
    aprobada: "Aprobada",
    rechazada: "Rechazada",
    cancelada: "Cancelada",
  }[estado];
}

export function opcionesEstados(solicitudes: SolicitudReservaAula[]): EstadoSolicitudAula[] {
  const presentes = new Set(solicitudes.map((s) => s.estado));
  return ESTADOS.filter((estado) => presentes.has(estado));
}

/** Acota las solicitudes por los filtros activos; texto sin acentos/mayúsculas. */
export function aplicarFiltrosSolicitudes(
  solicitudes: SolicitudReservaAula[],
  filtros: FiltrosSolicitudesState,
): SolicitudReservaAula[] {
  const dia = normalizarTexto(filtros.dia);
  const materia = normalizarTexto(filtros.materia);
  const comision = normalizarTexto(filtros.comision);
  const docente = normalizarTexto(filtros.docente);
  return solicitudes.filter((solicitud) => {
    if (dia && !normalizarTexto(fechaSolicitud(solicitud)).includes(dia)) return false;
    if (materia && !normalizarTexto(solicitud.materia.nombre).includes(materia)) return false;
    if (comision && !normalizarTexto(solicitud.comision).includes(comision)) return false;
    if (docente) {
      const nombreDocente = solicitud.docente
        ? `${solicitud.docente.nombre} ${solicitud.docente.apellido}`
        : "";
      if (!normalizarTexto(nombreDocente).includes(docente)) return false;
    }
    if (filtros.estado.length && !filtros.estado.includes(solicitud.estado)) return false;
    return true;
  });
}

function valorOrden(solicitud: SolicitudReservaAula, columna: ColumnaOrdenSolicitudes): string {
  switch (columna) {
    case "dia":
      return solicitud.dia;
    case "horario":
      return solicitud.horarioDesde;
    case "alumnos":
      return String(solicitud.cantidadAlumnosAprox).padStart(6, "0");
    case "materia":
      return solicitud.materia.nombre;
    case "comision":
      return solicitud.comision;
    case "estado":
      return etiquetaEstado(solicitud.estado);
    case "docente":
      return solicitud.docente ? `${solicitud.docente.apellido}, ${solicitud.docente.nombre}` : "";
  }
}

function compararTexto(a: string, b: string): number {
  const normalizadoA = normalizarTexto(a);
  const normalizadoB = normalizarTexto(b);
  if (!normalizadoA || !normalizadoB) return normalizadoA ? -1 : normalizadoB ? 1 : 0;
  return normalizadoA.localeCompare(normalizadoB, "es", { numeric: true });
}

export function ordenarSolicitudes(
  solicitudes: SolicitudReservaAula[],
  orden: OrdenSolicitudes | null,
): SolicitudReservaAula[] {
  if (!orden) return [...solicitudes];
  const signo = orden.direccion === "asc" ? 1 : -1;
  return [...solicitudes].sort((a, b) => {
    const valorA = valorOrden(a, orden.columna).trim();
    const valorB = valorOrden(b, orden.columna).trim();
    if (!valorA || !valorB) return valorA ? -1 : valorB ? 1 : 0;
    return compararTexto(valorA, valorB) * signo || a.id.localeCompare(b.id);
  });
}

export function aplicarFiltrosYOrdenSolicitudes(
  solicitudes: SolicitudReservaAula[],
  filtros: FiltrosSolicitudesState,
  orden: OrdenSolicitudes | null,
): SolicitudReservaAula[] {
  return ordenarSolicitudes(aplicarFiltrosSolicitudes(solicitudes, filtros), orden);
}

export function siguienteOrdenSolicitudes(
  orden: OrdenSolicitudes | null,
  columna: ColumnaOrdenSolicitudes,
): OrdenSolicitudes | null {
  if (!orden || orden.columna !== columna) return { columna, direccion: "asc" };
  if (orden.direccion === "asc") return { columna, direccion: "desc" };
  return null;
}
