import { apiClient } from "../../../shared/api/client";

/**
 * Tipos y llamada del feed unificado de auditoría (design D3, D5, D9,
 * sistema-seccion-unificada). Separado de `sistemaApi.ts` (que quedó sólo
 * con la salud de componentes, tasks.md 6.1) porque son dos consultas de
 * dominios distintos que antes convivían en el mismo archivo.
 *
 * BREAKING respecto de la versión anterior: `id` es `string`
 * (`cambios-<n>` | `asistente-<n>`), `schema`/`actor` se reemplazan por
 * `modulo`/`q`, y se agregan `origen`, `tipoActor`, `parcial` y
 * `fuentesNoDisponibles`. El único consumidor es este frontend, así que el
 * cambio va en el mismo diff que el backend.
 *
 * `FiltrosAuditoria` usa `rowPk`, el nombre exacto del parámetro del backend
 * (`ConsultaAuditoriaDto.RowPk`, `ModelosAdministracionSistema.cs`); el hook
 * `useFiltrosAuditoria` guarda ese mismo filtro en la URL bajo `clave` (design
 * D9) y lo traduce a `rowPk` recién acá, en la capa de la llamada HTTP.
 */

export type OrigenEvento = "cambios" | "asistente";
export type TipoActor = "persona" | "proceso" | "no_identificado";
export type AccionAuditoria = "INSERT" | "UPDATE" | "DELETE";

export interface FiltrosAuditoria {
  pagina: number;
  tamanoPagina: number;
  desde?: string;
  hasta?: string;
  accion?: AccionAuditoria;
  modulo?: string;
  tabla?: string;
  rowPk?: string;
  cambiadoPor?: string;
  q?: string;
}

export interface CambioAuditoria {
  campo: string;
  etiquetaCampo: string;
  valorAnterior: string | null;
  valorNuevo: string | null;
  oculto: boolean;
}

export interface EventoAuditoria {
  id: string;
  origen: OrigenEvento;
  schema: string;
  tabla: string;
  /** Nunca `null` en la red: el backend manda el literal «—» cuando la fuente no registra clave. */
  rowPk: string;
  accion: AccionAuditoria;
  cambiadoEn: string;
  cambiadoPor: string | null;
  requestId: string | null;
  columnasCambiadas: string[];
  cambios: CambioAuditoria[];
  actor: string;
  tipoActor: TipoActor;
  accionEtiqueta: string;
  modulo: string;
  objeto: string;
  resumen: string;
}

export interface PaginaAuditoria {
  elementos: EventoAuditoria[];
  pagina: number;
  tamanoPagina: number;
  total: number;
  parcial: boolean;
  fuentesNoDisponibles: string[];
}

export async function listarAuditoria(filtros: FiltrosAuditoria): Promise<PaginaAuditoria> {
  const { data } = await apiClient.get<PaginaAuditoria>("/api/administracion/auditoria", {
    params: filtros,
  });
  return data;
}
