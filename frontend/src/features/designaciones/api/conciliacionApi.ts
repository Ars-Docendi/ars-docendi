import { apiClient } from "../../../shared/api/client";
import type { EstadoPedido, Novedad, PeriodoDesignacion } from "../types";

/** Cargo, dedicación y horas de una designación vigente o de lo que pide un pedido. */
export interface ValoresDesignacion {
  cargo: string | null;
  dedicacion: string | null;
  horas: number | null;
  horasInvestigacion: number | null;
  horasExternas: number | null;
}

export interface DesignacionVigente {
  materiaId: string;
  materia: string;
  carreraId: string;
  carrera: string;
  valores: ValoresDesignacion;
}

/** Acciones que el backend habilita sobre el pedido para el actor. */
export type AccionConciliacion = "sincronizar" | "rechazar" | "editar";

export interface PedidoConciliacion {
  id: string;
  numero: string;
  version: number;
  novedad: Novedad;
  estado: Extract<EstadoPedido, "en_lote" | "no_aprobado" | "aprobado">;
  materiaId: string;
  materia: string;
  carreraId: string;
  carrera: string;
  /** Lo que tiene hoy en la materia del pedido; nulo en un Alta. */
  hoy: ValoresDesignacion | null;
  /** Lo que pide el pedido; nulo en una Baja. */
  queda: ValoresDesignacion | null;
  justificacion: string | null;
  tipoBaja: string | null;
  tipoBajaDetalle: string | null;
  motivoRechazo: string | null;
  sincronizadoEn: string | null;
  sincronizadoPor: string | null;
  acciones: AccionConciliacion[];
}

/** Una fila por pedido: la pantalla los agrupa por docente. */
export interface EntradaConciliacion {
  personaId: string;
  nombre: string;
  apellido: string;
  cuil: string | null;
  documento: string;
  personaNueva: boolean;
  pedido: PedidoConciliacion;
  vigentes: DesignacionVigente[];
}

export interface Conciliacion {
  periodo: PeriodoDesignacion | null;
  docentes: EntradaConciliacion[];
}

export async function consultarConciliacion(): Promise<Conciliacion> {
  return (await apiClient.get<Conciliacion>("/api/designaciones/conciliacion")).data;
}

/**
 * Sincroniza los pedidos aceptados con la versión que se vio. Todo o nada: si alguno cambió,
 * el backend responde 409 y no aplica ninguno.
 */
export async function sincronizarConciliacion(
  pedidos: { id: string; version: number }[],
  clave: string,
): Promise<{ numeros: string[] }> {
  return (
    await apiClient.post<{ numeros: string[] }>(
      "/api/designaciones/conciliacion/sincronizar",
      { pedidos },
      { headers: { "Idempotency-Key": clave } },
    )
  ).data;
}

/** Rechaza el pedido en la conciliación: sale del Excel y queda editable. */
export async function rechazarEnConciliacion(id: string, motivo: string): Promise<void> {
  await apiClient.post(
    `/api/designaciones/pedidos/${id}/no-aprobar`,
    { comentario: motivo },
    { headers: { "Idempotency-Key": crypto.randomUUID() } },
  );
}
