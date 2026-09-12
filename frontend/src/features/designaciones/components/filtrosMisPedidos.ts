import type { EstadoPedido, Novedad, PedidoDesignacion } from "../types";

export type FiltroEstado =
  | "borrador"
  | "revision"
  | "aprobado"
  | "rechazado"
  | "devuelto"
  | "cancelado";

export interface FiltrosMisPedidosState {
  docente: string;
  numero: string;
  legajo: string;
  catedra: string;
  enviado: string;
  tipo: Novedad[];
  estado: FiltroEstado[];
}

export const FILTROS_INICIALES: FiltrosMisPedidosState = {
  docente: "",
  numero: "",
  legajo: "",
  catedra: "",
  enviado: "",
  tipo: [],
  estado: [],
};

export type ColumnaOrdenMisPedidos =
  | "numero"
  | "docente"
  | "legajo"
  | "catedra"
  | "tipo"
  | "enviado"
  | "estado";

export interface OrdenMisPedidos {
  columna: ColumnaOrdenMisPedidos;
  direccion: "asc" | "desc";
}

const ESTADOS: FiltroEstado[] = [
  "borrador",
  "revision",
  "aprobado",
  "devuelto",
  "rechazado",
  "cancelado",
];

/** Minúsculas y sin diacríticos, para comparar sin distinguir mayúsculas/acentos. */
export function normalizarTexto(texto: string): string {
  return texto
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "");
}

export function etiquetaNovedadCorta(pedido: PedidoDesignacion): string {
  if (pedido.novedad !== "Cambio de cargo o dedicación") return pedido.novedad;
  if (pedido.cargoSolicitado && pedido.cargoSolicitado !== pedido.cargoActual)
    return "Cambio de cargo";
  if (pedido.dedicacionSolicitada && pedido.dedicacionSolicitada !== pedido.dedicacionActual)
    return "Cambio de dedicación";
  return "Cambio de cargo o dedicación";
}

export function fechaEnviadoIso(pedido: PedidoDesignacion): string | undefined {
  return (
    pedido.historial.find((evento) => evento.accion === "enviar")?.fecha ??
    pedido.historial.find((evento) => evento.accion === "crear")?.fecha ??
    pedido.historial.at(0)?.fecha
  );
}

/** Fecha de envío (o de creación si sigue en borrador), formato dd/mm/aaaa. */
export function fechaEnviado(pedido: PedidoDesignacion): string {
  const iso = fechaEnviadoIso(pedido);
  if (!iso) return "—";
  const fecha = new Date(iso);
  const dia = String(fecha.getUTCDate()).padStart(2, "0");
  const mes = String(fecha.getUTCMonth() + 1).padStart(2, "0");
  return `${dia}/${mes}/${fecha.getUTCFullYear()}`;
}

function coincideEstado(estado: EstadoPedido, filtro: FiltroEstado): boolean {
  switch (filtro) {
    case "borrador":
      return estado === "borrador";
    case "revision":
      return estado.startsWith("en_revision");
    case "aprobado":
      return estado === "en_lote";
    case "rechazado":
      return estado === "rechazado";
    case "devuelto":
      return estado === "devuelto";
    case "cancelado":
      return estado === "cancelado";
  }
}

export function etiquetaEstadoFiltro(filtro: FiltroEstado): string {
  return {
    borrador: "Borrador",
    revision: "En revisión",
    aprobado: "Aprobado",
    rechazado: "Rechazado",
    devuelto: "Devuelto",
    cancelado: "Cancelado",
  }[filtro];
}

export function opcionesTiposMisPedidos(pedidos: PedidoDesignacion[]): Novedad[] {
  const tipos = new Set(pedidos.map((pedido) => pedido.novedad));
  return ["Alta", "Baja", "Cambio de cargo o dedicación"].filter((tipo) =>
    tipos.has(tipo as Novedad),
  ) as Novedad[];
}

export function opcionesEstadosMisPedidos(pedidos: PedidoDesignacion[]): FiltroEstado[] {
  const estados = new Set(
    pedidos.map((pedido) => ESTADOS.find((filtro) => coincideEstado(pedido.estado, filtro))),
  );
  return ESTADOS.filter((estado) => estados.has(estado));
}

/** Acota los pedidos por los filtros activos; texto sin acentos/mayúsculas. */
export function aplicarFiltrosMisPedidos(
  pedidos: PedidoDesignacion[],
  filtros: FiltrosMisPedidosState,
): PedidoDesignacion[] {
  const docente = normalizarTexto(filtros.docente);
  const numero = normalizarTexto(filtros.numero);
  const legajo = normalizarTexto(filtros.legajo);
  const catedra = normalizarTexto(filtros.catedra);
  const enviado = normalizarTexto(filtros.enviado);
  return pedidos.filter((pedido) => {
    if (docente && !normalizarTexto(pedido.docente.nombre).includes(docente)) return false;
    if (numero && !normalizarTexto(pedido.numero ?? "").includes(numero)) return false;
    if (legajo && !normalizarTexto(pedido.docente.legajo ?? "").includes(legajo)) return false;
    if (catedra && !normalizarTexto(pedido.catedra).includes(catedra)) return false;
    if (
      enviado &&
      ![fechaEnviado(pedido), fechaEnviadoIso(pedido) ?? ""].some((valor) =>
        normalizarTexto(valor).includes(enviado),
      )
    )
      return false;
    if (filtros.tipo.length && !filtros.tipo.includes(pedido.novedad)) return false;
    if (
      filtros.estado.length &&
      !filtros.estado.some((filtro) => coincideEstado(pedido.estado, filtro))
    )
      return false;
    return true;
  });
}

function valorOrden(pedido: PedidoDesignacion, columna: ColumnaOrdenMisPedidos): string {
  switch (columna) {
    case "numero":
      return pedido.numero ?? "";
    case "docente":
      return pedido.docente.nombre;
    case "legajo":
      return pedido.docente.legajo ?? "";
    case "catedra":
      return pedido.catedra;
    case "tipo":
      return etiquetaNovedadCorta(pedido);
    case "enviado":
      return fechaEnviadoIso(pedido) ?? "";
    case "estado":
      return etiquetaEstadoFiltro(
        ESTADOS.find((filtro) => coincideEstado(pedido.estado, filtro)) ?? "borrador",
      );
  }
}

function compararTexto(a: string, b: string): number {
  const normalizadoA = normalizarTexto(a);
  const normalizadoB = normalizarTexto(b);
  if (!normalizadoA || !normalizadoB) return normalizadoA ? -1 : normalizadoB ? 1 : 0;
  return normalizadoA.localeCompare(normalizadoB, "es", { numeric: true });
}

function compararNumero(a: string, b: string): number {
  if (/^\d+$/.test(a.trim()) && /^\d+$/.test(b.trim())) return Number(a) - Number(b);
  return compararTexto(a, b);
}

function compararFecha(a: string, b: string): number {
  if (!a || !b) return a ? -1 : b ? 1 : 0;
  return Date.parse(a) - Date.parse(b);
}

export function compararPedidos(
  a: PedidoDesignacion,
  b: PedidoDesignacion,
  columna: ColumnaOrdenMisPedidos,
): number {
  const valorA = valorOrden(a, columna);
  const valorB = valorOrden(b, columna);
  if (columna === "enviado") return compararFecha(valorA, valorB);
  if (columna === "numero" || columna === "legajo") return compararNumero(valorA, valorB);
  return compararTexto(valorA, valorB);
}

export function ordenarMisPedidos(
  pedidos: PedidoDesignacion[],
  orden: OrdenMisPedidos | null,
): PedidoDesignacion[] {
  if (!orden) return [...pedidos];
  const signo = orden.direccion === "asc" ? 1 : -1;
  return [...pedidos].sort((a, b) => {
    const valorA = valorOrden(a, orden.columna).trim();
    const valorB = valorOrden(b, orden.columna).trim();
    if (!valorA || !valorB) return valorA ? -1 : valorB ? 1 : 0;
    return compararPedidos(a, b, orden.columna) * signo || a.id.localeCompare(b.id);
  });
}

export function aplicarFiltrosYOrdenMisPedidos(
  pedidos: PedidoDesignacion[],
  filtros: FiltrosMisPedidosState,
  orden: OrdenMisPedidos | null,
): PedidoDesignacion[] {
  return ordenarMisPedidos(aplicarFiltrosMisPedidos(pedidos, filtros), orden);
}

export function siguienteOrdenMisPedidos(
  orden: OrdenMisPedidos | null,
  columna: ColumnaOrdenMisPedidos,
): OrdenMisPedidos | null {
  if (!orden || orden.columna !== columna) return { columna, direccion: "asc" };
  if (orden.direccion === "asc") return { columna, direccion: "desc" };
  return null;
}
