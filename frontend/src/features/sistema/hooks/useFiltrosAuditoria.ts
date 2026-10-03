import { useLocation, useNavigate } from "react-router-dom";

import type { Periodo } from "../utils/zonaHoraria";
import type { AccionAuditoria } from "../api/auditoriaApi";

export type FiltroAccion = "" | AccionAuditoria;

export interface EstadoFiltrosAuditoria {
  q: string;
  periodo: Periodo;
  accion: FiltroAccion;
  /** «» significa «Todos»; si no, el label del chip (design D13). */
  modulo: string;
  /** `datetime-local`, vacío = sin cota. */
  desde: string;
  hasta: string;
  tabla: string;
  clave: string;
  pagina: number;
  /** El id del evento con el detalle abierto, o `null`. */
  evento: string | null;
}

const PERIODO_DEFAULT: Periodo = "7d";

const DEFAULT_SIN_PAGINA_NI_EVENTO: Omit<EstadoFiltrosAuditoria, "pagina" | "evento"> = {
  q: "",
  periodo: PERIODO_DEFAULT,
  accion: "",
  modulo: "",
  desde: "",
  hasta: "",
  tabla: "",
  clave: "",
};

function esPeriodo(valor: string | null): valor is Periodo {
  return valor === "hoy" || valor === "7d" || valor === "30d" || valor === "todo";
}

function esAccion(valor: string | null): valor is AccionAuditoria {
  return valor === "INSERT" || valor === "UPDATE" || valor === "DELETE";
}

function leerFiltros(params: URLSearchParams): EstadoFiltrosAuditoria {
  const periodoCrudo = params.get("periodo");
  const accionCruda = params.get("accion");
  const pagina = Number(params.get("pagina"));
  return {
    q: params.get("q") ?? "",
    periodo: esPeriodo(periodoCrudo) ? periodoCrudo : PERIODO_DEFAULT,
    accion: esAccion(accionCruda) ? accionCruda : "",
    modulo: params.get("modulo") ?? "",
    desde: params.get("desde") ?? "",
    hasta: params.get("hasta") ?? "",
    tabla: params.get("tabla") ?? "",
    clave: params.get("clave") ?? "",
    pagina: Number.isInteger(pagina) && pagina > 0 ? pagina : 1,
    evento: params.get("evento"),
  };
}

function escribirFiltros(filtros: EstadoFiltrosAuditoria): URLSearchParams {
  const params = new URLSearchParams();
  if (filtros.q) params.set("q", filtros.q);
  if (filtros.periodo !== PERIODO_DEFAULT) params.set("periodo", filtros.periodo);
  if (filtros.accion) params.set("accion", filtros.accion);
  if (filtros.modulo) params.set("modulo", filtros.modulo);
  if (filtros.desde) params.set("desde", filtros.desde);
  if (filtros.hasta) params.set("hasta", filtros.hasta);
  if (filtros.tabla) params.set("tabla", filtros.tabla);
  if (filtros.clave) params.set("clave", filtros.clave);
  if (filtros.pagina !== 1) params.set("pagina", String(filtros.pagina));
  if (filtros.evento) params.set("evento", filtros.evento);
  return params;
}

export interface UseFiltrosAuditoriaResultado {
  filtros: EstadoFiltrosAuditoria;
  /** Cambia uno o más filtros (no la página ni el evento) y vuelve a la página 1. */
  actualizarFiltros: (parcial: Partial<Omit<EstadoFiltrosAuditoria, "pagina" | "evento">>) => void;
  irAPagina: (pagina: number) => void;
  /** Vuelve todo a su default, incluido el detalle abierto. */
  limpiarFiltros: () => void;
  /** Abre (id) o cierra (`null`) el panel de detalle sin tocar el resto. */
  abrirEvento: (id: string | null) => void;
  /** «difiere del default» (requisito «Auditoría tab filters...»): gatea «Limpiar filtros». */
  difiereDelDefault: boolean;
  /** El badge de «Más filtros»: cuenta sólo Desde/Hasta/Tabla/Clave. */
  cantidadFiltrosAvanzados: number;
}

/**
 * Filtros de auditoría dueños de la URL (design D9, sistema-seccion-
 * unificada): cada filtro vive en el query string con sus defaults omitidos,
 * así que una URL compartida reproduce exactamente la misma vista.
 *
 * No usa `useSearchParams` de react-router: su `setSearchParams` navega con
 * `navigate("?" + params)`, una cadena sin `hash`, y `resolveTo` trata un
 * segmento ausente como vacío en vez de "mantener el actual" — cada cambio de
 * filtro borraba el hash de la pestaña activa. `useSeccionSistema` entonces
 * veía un hash inválido y corregía a la primera pestaña permitida, así que
 * cualquier interacción de Auditoría (chip, búsqueda, fila) expulsaba a
 * Estado y de paso perdía los filtros recién aplicados (bug real-app, task
 * 11.4). Acá se arma la navegación a mano para preservar `location.hash`.
 */
export function useFiltrosAuditoria(): UseFiltrosAuditoriaResultado {
  const location = useLocation();
  const navigate = useNavigate();
  const searchParams = new URLSearchParams(location.search);
  const filtros = leerFiltros(searchParams);

  function navegarAFiltros(siguientes: EstadoFiltrosAuditoria, opciones?: { replace?: boolean }) {
    const params = escribirFiltros(siguientes);
    const search = params.toString();
    navigate(
      { search: search ? `?${search}` : "", hash: location.hash },
      { replace: opciones?.replace ?? false },
    );
  }

  function actualizarFiltros(parcial: Partial<Omit<EstadoFiltrosAuditoria, "pagina" | "evento">>) {
    navegarAFiltros({ ...filtros, ...parcial, pagina: 1 });
  }

  function irAPagina(pagina: number) {
    navegarAFiltros({ ...filtros, pagina });
  }

  function limpiarFiltros() {
    navegarAFiltros({ ...DEFAULT_SIN_PAGINA_NI_EVENTO, pagina: 1, evento: null });
  }

  function abrirEvento(id: string | null) {
    navegarAFiltros({ ...filtros, evento: id }, { replace: true });
  }

  const difiereDelDefault =
    filtros.q !== DEFAULT_SIN_PAGINA_NI_EVENTO.q ||
    filtros.periodo !== DEFAULT_SIN_PAGINA_NI_EVENTO.periodo ||
    filtros.accion !== DEFAULT_SIN_PAGINA_NI_EVENTO.accion ||
    filtros.modulo !== DEFAULT_SIN_PAGINA_NI_EVENTO.modulo ||
    filtros.desde !== DEFAULT_SIN_PAGINA_NI_EVENTO.desde ||
    filtros.hasta !== DEFAULT_SIN_PAGINA_NI_EVENTO.hasta ||
    filtros.tabla !== DEFAULT_SIN_PAGINA_NI_EVENTO.tabla ||
    filtros.clave !== DEFAULT_SIN_PAGINA_NI_EVENTO.clave;

  const cantidadFiltrosAvanzados = [
    filtros.desde,
    filtros.hasta,
    filtros.tabla,
    filtros.clave,
  ].filter((v) => v !== "").length;

  return {
    filtros,
    actualizarFiltros,
    irAPagina,
    limpiarFiltros,
    abrirEvento,
    difiereDelDefault,
    cantidadFiltrosAvanzados,
  };
}
