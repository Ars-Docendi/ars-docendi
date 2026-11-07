import type {
  DesignacionVigente,
  EntradaConciliacion,
  PedidoConciliacion,
  ValoresDesignacion,
} from "../api/conciliacionApi";
import type { Novedad } from "../types";

/** Un docente de la conciliación con todos sus pedidos del período activo. */
export interface DocenteConciliacion {
  personaId: string;
  nombre: string;
  apellido: string;
  cuil: string | null;
  documento: string;
  personaNueva: boolean;
  vigentes: DesignacionVigente[];
  pedidos: PedidoConciliacion[];
}

export type PestanaConciliacion = "pendientes" | "rechazados" | "sincronizados";

export interface FiltrosConciliacion {
  texto: string;
  novedad: Novedad | "";
  carrera: string;
}

export const FILTROS_CONCILIACION_INICIALES: FiltrosConciliacion = {
  texto: "",
  novedad: "",
  carrera: "",
};

/** La API devuelve una entrada por pedido; la pantalla trabaja por docente. */
export function agruparPorDocente(entradas: EntradaConciliacion[]): DocenteConciliacion[] {
  const porPersona = new Map<string, DocenteConciliacion>();
  for (const entrada of entradas) {
    const docente = porPersona.get(entrada.personaId);
    if (docente) {
      docente.pedidos.push(entrada.pedido);
      docente.personaNueva ||= entrada.personaNueva;
    } else {
      porPersona.set(entrada.personaId, {
        personaId: entrada.personaId,
        nombre: entrada.nombre,
        apellido: entrada.apellido,
        cuil: entrada.cuil,
        documento: entrada.documento,
        personaNueva: entrada.personaNueva,
        vigentes: entrada.vigentes,
        pedidos: [entrada.pedido],
      });
    }
  }
  return [...porPersona.values()];
}

export const nombreCompleto = (docente: DocenteConciliacion) =>
  `${docente.apellido}, ${docente.nombre}`;

/** Pedidos en lote del docente: los que se aceptan y sincronizan juntos. */
export const pendientes = (docente: DocenteConciliacion) =>
  docente.pedidos.filter((pedido) => pedido.estado === "en_lote");

/** Se puede tildar sólo si el actor puede sincronizar todos sus pedidos en lote. */
export const aceptable = (docente: DocenteConciliacion) => {
  const lista = pendientes(docente);
  return lista.length > 0 && lista.every((pedido) => pedido.acciones.includes("sincronizar"));
};

export interface DesignacionResultante {
  materiaId: string;
  materia: string;
  carrera: string;
  valores: ValoresDesignacion;
}

/**
 * Cómo queda el docente si se sincronizan sus pedidos en lote. Los rechazados no aplican y los
 * ya sincronizados están en las vigentes.
 */
export function resultado(docente: DocenteConciliacion): DesignacionResultante[] {
  const porMateria = new Map<string, DesignacionResultante>(
    docente.vigentes.map((vigente) => [vigente.materiaId, { ...vigente }]),
  );
  for (const pedido of pendientes(docente)) {
    if (pedido.novedad === "Baja") porMateria.delete(pedido.materiaId);
    else if (pedido.queda)
      porMateria.set(pedido.materiaId, {
        materiaId: pedido.materiaId,
        materia: pedido.materia,
        carrera: pedido.carrera,
        valores: pedido.queda,
      });
  }
  return [...porMateria.values()];
}

/** Carreras en las que el docente tiene designaciones o pedidos sin sincronizar. */
export function carrerasDe(docente: DocenteConciliacion): string[] {
  return [
    ...new Set([
      ...docente.vigentes.map((vigente) => vigente.carrera),
      ...docente.pedidos
        .filter((pedido) => pedido.estado !== "aprobado")
        .map((pedido) => pedido.carrera),
    ]),
  ]
    .filter(Boolean)
    .sort((a, b) => a.localeCompare(b, "es"));
}

/** Vigentes en materias sin un pedido abierto: el detalle las muestra "Sin cambios". */
export const sinCambios = (docente: DocenteConciliacion) =>
  docente.vigentes.filter(
    (vigente) =>
      !docente.pedidos.some(
        (pedido) => pedido.materiaId === vigente.materiaId && pedido.estado !== "aprobado",
      ),
  );

function coincideDocente(docente: DocenteConciliacion, texto: string): boolean {
  const buscado = texto.trim().toLocaleLowerCase("es");
  if (!buscado) return true;
  const digitos = buscado.replace(/\D/g, "");
  return (
    nombreCompleto(docente).toLocaleLowerCase("es").includes(buscado) ||
    `${docente.nombre} ${docente.apellido}`.toLocaleLowerCase("es").includes(buscado) ||
    (digitos.length > 0 &&
      ((docente.cuil ?? "").replace(/\D/g, "").includes(digitos) ||
        docente.documento.includes(digitos)))
  );
}

/** ¿El docente pasa los filtros, mirando sólo los pedidos de la pestaña? */
export function pasaFiltros(
  docente: DocenteConciliacion,
  pedidos: PedidoConciliacion[],
  filtros: FiltrosConciliacion,
): boolean {
  if (!coincideDocente(docente, filtros.texto)) return false;
  if (filtros.carrera && !carrerasDe(docente).includes(filtros.carrera)) return false;
  if (filtros.novedad && !pedidos.some((pedido) => pedido.novedad === filtros.novedad))
    return false;
  return true;
}

export const ESTADO_DE_PESTANA = {
  rechazados: "no_aprobado",
  sincronizados: "aprobado",
} as const;

/** Filas (docente, pedido) de las pestañas Rechazados y Sincronizados. */
export function filasDe(
  docentes: DocenteConciliacion[],
  pestana: "rechazados" | "sincronizados",
  filtros: FiltrosConciliacion,
): { docente: DocenteConciliacion; pedido: PedidoConciliacion }[] {
  const estado = ESTADO_DE_PESTANA[pestana];
  return docentes.flatMap((docente) =>
    docente.pedidos
      .filter((pedido) => pedido.estado === estado && pasaFiltros(docente, [pedido], filtros))
      .map((pedido) => ({ docente, pedido })),
  );
}

export interface ResumenSincronizacion {
  personasNuevas: number;
  altas: number;
  cambios: number;
  bajas: number;
  pedidos: { id: string; version: number }[];
}

export function resumenSincronizacion(aceptados: DocenteConciliacion[]): ResumenSincronizacion {
  const lista = aceptados.flatMap(pendientes);
  return {
    personasNuevas: aceptados.filter((docente) => docente.personaNueva).length,
    altas: lista.filter((pedido) => pedido.novedad === "Alta").length,
    cambios: lista.filter((pedido) => pedido.novedad === "Cambio de cargo o dedicación").length,
    bajas: lista.filter((pedido) => pedido.novedad === "Baja").length,
    pedidos: lista.map((pedido) => ({ id: pedido.id, version: pedido.version })),
  };
}

/** "JTP Simple, 10 h" (+ investigación y externas cuando hay). */
export function describirValores(valores: ValoresDesignacion): string {
  const base = [valores.cargo, valores.dedicacion].filter(Boolean).join(" ");
  const partes = [`${base}, ${valores.horas ?? 0} h`];
  if (valores.horasInvestigacion) partes.push(`investigación ${valores.horasInvestigacion} h`);
  if (valores.horasExternas) partes.push(`externas ${valores.horasExternas} h`);
  return partes.join(", ");
}

export const tipoBajaTexto = (pedido: PedidoConciliacion) =>
  pedido.tipoBaja === "Otro" && pedido.tipoBajaDetalle
    ? `Otro: ${pedido.tipoBajaDetalle}`
    : (pedido.tipoBaja ?? "");

/** Línea del preview de sincronización de un pedido. */
export function lineaPreview(pedido: PedidoConciliacion): string {
  const donde = [pedido.materia, pedido.carrera].filter(Boolean).join(", ");
  if (pedido.novedad === "Baja") {
    const tipo = tipoBajaTexto(pedido);
    return `Baja en ${donde}${tipo ? ` (${tipo})` : ""}${pedido.hoy ? `: se cierra ${describirValores(pedido.hoy)}` : ""}`;
  }
  if (pedido.novedad === "Alta" || !pedido.hoy)
    return `Alta en ${donde}: ${pedido.queda ? describirValores(pedido.queda) : ""}`;
  return `Cambio en ${donde}: ${describirValores(pedido.hoy)} pasa a ${pedido.queda ? describirValores(pedido.queda) : ""}`;
}

export const plural = (n: number, singular: string, varios: string) =>
  `${n} ${n === 1 ? singular : varios}`;
