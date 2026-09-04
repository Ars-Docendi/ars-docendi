// ============================================================
// Modelo de presentación de la Tabla de revisión: los pedidos se reparten en
// PESTAÑAS — Mi bandeja / En Coordinación / En Secretaría / En Decanato /
// Finalizados — sobre UNA sola tabla. Antes eran 4 secciones desplegables, cada
// una con su propio head de columnas: repetían el head 4 veces, impedían
// comparar u ordenar entre etapas y le sacaban el trabajo a la columna Estado
// (dentro de "En Coordinación", decir "En Coordinación" no aportaba nada). Las
// pestañas conservan lo único que las secciones daban de verdad —el conteo por
// etapa y arrancar parado en la propia— con un solo head y una sola tabla.
//
// Cada pestaña de área contesta una sola pregunta: qué pedidos están HOY ahí. Un
// pedido en revisión está en el área que lo revisa; un pedido Devuelto está en el
// área que lo tiene que corregir (`propietarioActual`), NO en la etapa a la que
// volverá al reenviarse (`etapaRetorno`). Por eso existe "En Cátedra": la Cátedra
// no revisa nada, pero sí retiene los pedidos que Coordinación le devolvió, y
// antes esos aparecían bajo "En Coordinación" como si el Coordinador los tuviera.
// "Todos" es la pestaña sin agrupar: todo lo que entró al circuito, en una lista.
//
// Lógica de vista pura (no es dominio): no decide autoridad, solo presenta — el
// gating por ámbito [BR-009] ya se aplicó a los `pedidos` que llegan acá.
// ============================================================
import type { ActorContexto, EstadoPedido, Novedad, PedidoDesignacion, Rol } from "../types";
import { puedeRevisar } from "../api/maquinaEstados";
import { formatearFecha } from "./detalleAdapters";

type EtapaRevision = "en_revision_coordinador" | "en_revision_secretaria" | "en_revision_decanato";

/** Id de cada pestaña de la Tabla. */
export type IdPestania =
  | "todos"
  | "en-catedra"
  | "en-coordinacion"
  | "en-secretaria"
  | "en-decanato"
  | "finalizados";

interface AreaDelCircuito {
  id: IdPestania;
  etiqueta: string;
  /** Rol que tiene el pedido cuando está en esta área. */
  rol: Rol;
  /** Etapa de revisión del área. La Cátedra no revisa: solo corrige devoluciones. */
  etapa?: EtapaRevision;
}

/**
 * Las áreas del circuito en orden, que son también las pestañas de etapa. La
 * Cátedra es un área sin etapa de revisión: no revisa nada, pero SÍ tiene
 * pedidos —los que le devolvió Coordinación, que están esperando que los corrija
 * y los reenvíe—. Sin esta pestaña, esos pedidos aparecían bajo "En
 * Coordinación", como si el Coordinador todavía los tuviera.
 */
const AREAS: AreaDelCircuito[] = [
  { id: "en-catedra", etiqueta: "En Cátedra", rol: "Jefe de Cátedra" },
  {
    id: "en-coordinacion",
    etiqueta: "En Coordinación",
    rol: "Coordinador",
    etapa: "en_revision_coordinador",
  },
  {
    id: "en-secretaria",
    etiqueta: "En Secretaría",
    rol: "Secretaría",
    etapa: "en_revision_secretaria",
  },
  { id: "en-decanato", etiqueta: "En Decanato", rol: "Decanato", etapa: "en_revision_decanato" },
];

export interface Pestania {
  id: IdPestania;
  etiqueta: string;
}

/** Las pestañas, en orden: la lista sin agrupar, el circuito de punta a punta, y el cierre. */
export const PESTANIAS: Pestania[] = [
  { id: "todos", etiqueta: "Todos" },
  ...AREAS.map(({ id, etiqueta }) => ({ id, etiqueta })),
  { id: "finalizados", etiqueta: "Finalizados" },
];

/**
 * Pestaña en la que abre la Tabla: el área propia del actor (Coordinador → "En
 * Coordinación", etc.). Administración no tiene área propia —ve todo el
 * departamento por igual—, así que abre en "Todos".
 */
export function pestaniaInicial(actor: ActorContexto): IdPestania {
  return AREAS.find((area) => area.rol === actor.rol)?.id ?? "todos";
}

/** ¿Es el turno del actor sobre este pedido? (revisor de la etapa en su ámbito, o Administración). */
export function esTuTurno(pedido: PedidoDesignacion, actor: ActorContexto): boolean {
  return puedeRevisar(pedido, actor);
}

/**
 * ¿Este pedido está HOY en esta área? Dos casos: lo está revisando (su estado es
 * la etapa del área) o se lo devolvieron para corregir (`propietarioActual`).
 *
 * Un devuelto se ubica por `propietarioActual` —quién lo tiene— y NO por
 * `etapaRetorno` —a dónde volverá al reenviarse—. Son cosas distintas y la
 * pestaña tiene que decir dónde está: un pedido que Coordinación devolvió a la
 * Cátedra volverá a Coordinación, pero mientras tanto lo tiene la Cátedra.
 */
function perteneceAArea(pedido: PedidoDesignacion, area: AreaDelCircuito): boolean {
  if (pedido.estado === "devuelto") return pedido.propietarioActual === area.rol;
  return area.etapa !== undefined && pedido.estado === area.etapa;
}

/** Fecha (epoch ms) del evento más reciente del historial; 0 si no hay historial. */
function fechaUltimaActualizacionMs(pedido: PedidoDesignacion): number {
  const ultimo = pedido.historial.at(-1);
  return ultimo ? new Date(ultimo.fecha).getTime() : 0;
}

/**
 * Orden dentro de una pestaña: prioritarios primero, después devueltos, después
 * el resto — dentro de cada grupo, por fecha de última actualización ascendente
 * (el que espera hace más tiempo, arriba).
 */
function compararEnArea(a: PedidoDesignacion, b: PedidoDesignacion): number {
  const rango = (p: PedidoDesignacion): number => {
    if (p.prioritario) return 0;
    if (p.estado === "devuelto") return 1;
    return 2;
  };
  const diff = rango(a) - rango(b);
  if (diff !== 0) return diff;
  return fechaUltimaActualizacionMs(a) - fechaUltimaActualizacionMs(b);
}

/**
 * Orden dentro de Finalizados: Aceptados antes que Rechazados; dentro de
 * cada bloque, por fecha de última actualización descendente (el cierre
 * más reciente arriba).
 */
function compararFinalizados(a: PedidoDesignacion, b: PedidoDesignacion): number {
  const rango = (p: PedidoDesignacion) => (p.estado === "en_lote" ? 0 : 1);
  const diff = rango(a) - rango(b);
  if (diff !== 0) return diff;
  return fechaUltimaActualizacionMs(b) - fechaUltimaActualizacionMs(a);
}

/** Estados terminales: el pedido ya no se mueve. Van todos a "Finalizados". */
const ESTADOS_TERMINALES: EstadoPedido[] = ["en_lote", "rechazado", "cancelado"];

/**
 * Pedidos de una pestaña, ya ordenados. Las de etapa agrupan por dónde está
 * trabado el pedido (activo en esa etapa, o devuelto esperando volver a ella);
 * "Finalizados" junta los terminales; "Todos" es la lista sin agrupar.
 *
 * "Todos" excluye los borradores: un borrador todavía no entró al circuito, es
 * del Jefe de Cátedra que lo está escribiendo y no hay nada que revisar. Fuera
 * de eso NO filtra nada, así que ningún pedido del ámbito queda invisible — los
 * `cancelado`, que antes no caían en ninguna sección, ahora se ven acá y en
 * Finalizados. El gating por ámbito [BR-009] ya se aplicó antes de llegar acá.
 */
export function pedidosDePestania(
  pedidos: PedidoDesignacion[],
  pestania: IdPestania,
): PedidoDesignacion[] {
  if (pestania === "todos") {
    return pedidos.filter((pedido) => pedido.estado !== "borrador").sort(compararEnArea);
  }
  if (pestania === "finalizados") {
    return pedidos
      .filter((pedido) => ESTADOS_TERMINALES.includes(pedido.estado))
      .sort(compararFinalizados);
  }
  const area = AREAS.find((a) => a.id === pestania);
  if (!area) return [];
  return pedidos.filter((pedido) => perteneceAArea(pedido, area)).sort(compararEnArea);
}

/** Iniciales del docente (p. ej. "Ana Pérez" → "AP"). */
export function inicialesDocente(nombre: string): string {
  const partes = nombre
    .split(/\s+/)
    .map((parte) => parte.replace(/[^\p{L}]/gu, "").charAt(0))
    .filter(Boolean);
  return (partes[0] ?? "").concat(partes[partes.length - 1] ?? "").toUpperCase() || "?";
}

/**
 * Inicio del pedido: el primer `enviar` (entrada al circuito de revisión), NO el
 * `crear` del borrador — el tiempo que estuvo guardado sin enviar no es tiempo de
 * revisión. `null` si nunca se envió.
 */
export function inicioEnCircuito(pedido: PedidoDesignacion): string | null {
  const iso = pedido.historial.find((evento) => evento.accion === "enviar")?.fecha;
  return iso ? formatearFecha(iso) : null;
}

/** Fecha del evento más reciente del historial (cualquier acción). */
export function ultimaActualizacion(pedido: PedidoDesignacion): string | null {
  const iso = pedido.historial.at(-1)?.fecha;
  return iso ? formatearFecha(iso) : null;
}

/**
 * Nombre del área a cargo de un rol, con el mismo vocabulario que los headers de
 * sección ("En Coordinación", "En Secretaría"). Un pedido devuelto queda a cargo
 * del ÁREA, no de una persona: si vuelve a Secretaría se encarga Secretaría, sea
 * quien sea que lo tome.
 */
const AREA_DE_ROL: Record<Rol, string> = {
  "Jefe de Cátedra": "Cátedra",
  Coordinador: "Coordinación",
  Secretaría: "Secretaría",
  Decanato: "Decanato",
  Administración: "Administración",
  Docente: "el docente",
};

/** Área que tiene que corregir un pedido devuelto; `null` si el pedido no lo declara. */
export function areaQueCorrige(pedido: PedidoDesignacion): string | null {
  return pedido.propietarioActual ? AREA_DE_ROL[pedido.propietarioActual] : null;
}

/** Etiqueta corta de la novedad para el chip. */
export function etiquetaNovedad(novedad: Novedad): string {
  return novedad === "Cambio de cargo o dedicación" ? "Cambio" : novedad;
}

/** Último evento del historial del tipo dado (devolución / rechazo). */
function eventoDe(
  pedido: PedidoDesignacion,
  accion: "devolver" | "rechazar",
): PedidoDesignacion["historial"][number] | undefined {
  return [...pedido.historial].reverse().find((evento) => evento.accion === accion);
}

/** Motivo crudo del último rechazo (sin el prefijo "Rechazado:"), para destacarlo como cita en el detalle. */
export function motivoRechazo(pedido: PedidoDesignacion): string | undefined {
  return eventoDe(pedido, "rechazar")?.comentario;
}

/** Nombre de quien devolvió el pedido por última vez, para "Devuelto por {nombre}". */
export function quienDevolvio(pedido: PedidoDesignacion): string | undefined {
  return eventoDe(pedido, "devolver")?.porNombre;
}

/** Rol de quien devolvió el pedido por última vez, para "Devuelto por {nombre} ({rol})". */
export function rolDeQuienDevolvio(pedido: PedidoDesignacion): Rol | undefined {
  return eventoDe(pedido, "devolver")?.porRol;
}

/**
 * Etiqueta de la celda Estado: el estado desnudo, sin el área. El área tiene su
 * propia columna (`areaActual`), así que meterla también acá la repetiría en cada
 * fila — y dentro de una pestaña de área la repetiría por tercera vez.
 *
 * `undefined` = la etiqueta por defecto del `StatusBadge` ya sirve tal cual (los
 * estados terminales, que no viven en ningún área).
 */
export function etiquetaEstado(pedido: PedidoDesignacion): string | undefined {
  if (pedido.estado === "devuelto") return "Devuelto";
  if (AREAS.some((area) => area.etapa === pedido.estado)) return "En revisión";
  return undefined;
}

/**
 * Área donde está el pedido hoy, para la columna Área. Un pedido en revisión está
 * en el área que lo revisa; uno devuelto, en la que lo tiene que corregir
 * (`propietarioActual`) — mismo criterio que usa el reparto en pestañas, así que
 * columna y pestaña nunca se contradicen. `null` en los estados terminales: un
 * pedido cerrado ya no está en ninguna parte del circuito.
 */
export function areaActual(pedido: PedidoDesignacion): string | null {
  if (pedido.estado === "devuelto") return areaQueCorrige(pedido);
  const area = AREAS.find((a) => a.etapa === pedido.estado);
  return area ? AREA_DE_ROL[area.rol] : null;
}

// ============================================================
// Ordenamiento por columna (`Table.HeaderCell` de la librería ya trae el `th`
// clickeable, el `aria-sort` y la flechita: acá solo vive el criterio).
// ============================================================

export type ColumnaOrdenable = "docente" | "legajo" | "tipo" | "inicio" | "ultima" | "estado";

export interface OrdenTabla {
  columna: ColumnaOrdenable;
  direccion: "asc" | "desc";
}

/** Epoch ms del primer `enviar`; 0 si el pedido nunca se envió (queda primero al ordenar asc). */
function inicioMs(pedido: PedidoDesignacion): number {
  const iso = pedido.historial.find((evento) => evento.accion === "enviar")?.fecha;
  return iso ? new Date(iso).getTime() : 0;
}

/**
 * Valor comparable de cada columna. Las fechas se comparan en epoch ms y no por
 * su texto "dd/mm/aaaa", que ordenaría por día antes que por año.
 */
function valorDeOrden(pedido: PedidoDesignacion, columna: ColumnaOrdenable): string | number {
  switch (columna) {
    case "docente":
      return pedido.docente.nombre;
    case "legajo":
      return pedido.docente.legajo ?? "";
    case "tipo":
      return etiquetaNovedad(pedido.novedad);
    case "inicio":
      return inicioMs(pedido);
    case "ultima":
      return fechaUltimaActualizacionMs(pedido);
    case "estado":
      return pedido.estado;
  }
}

/**
 * Aplica el orden elegido por el usuario. `null` = sin orden manual, y ahí manda
 * el orden por defecto de la pestaña (prioritarios, devueltos, y después el que
 * espera hace más tiempo), que es información y no un capricho: por eso el ciclo
 * del header vuelve a `null` en el tercer clic en vez de quedarse en asc/desc.
 */
export function ordenarPedidos(
  pedidos: PedidoDesignacion[],
  orden: OrdenTabla | null,
): PedidoDesignacion[] {
  if (!orden) return pedidos;
  const signo = orden.direccion === "asc" ? 1 : -1;
  return [...pedidos].sort((a, b) => {
    const va = valorDeOrden(a, orden.columna);
    const vb = valorDeOrden(b, orden.columna);
    if (typeof va === "number" && typeof vb === "number") return (va - vb) * signo;
    // `numeric` para que los legajos "1005" y "999" ordenen como números.
    return String(va).localeCompare(String(vb), "es", { numeric: true }) * signo;
  });
}

/** Siguiente estado del ciclo del header: asc → desc → sin orden manual. */
export function siguienteOrden(
  actual: OrdenTabla | null,
  columna: ColumnaOrdenable,
): OrdenTabla | null {
  if (actual?.columna !== columna) return { columna, direccion: "asc" };
  if (actual.direccion === "asc") return { columna, direccion: "desc" };
  return null;
}
