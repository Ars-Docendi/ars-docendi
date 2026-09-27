import { isAxiosError } from "axios";

import { apiClient } from "../../../shared/api/client";

export type EstadoDisponibilidad = "disponible" | "no_disponible" | "desconocido";
export type MantenimientoAsistente = "activo" | "inactivo" | "desconocido";
export type MotivoFalla = "timeout" | "error";

export const COMPONENTES_PING = [
  { id: "aulas", nombre: "Aulas" },
  { id: "tareas", nombre: "Tareas" },
  { id: "designaciones", nombre: "Designaciones" },
  { id: "portal", nombre: "Portal" },
  { id: "asistente", nombre: "Asistente" },
] as const;

export type IdComponentePing = (typeof COMPONENTES_PING)[number]["id"];

export interface ResultadoPing {
  id: IdComponentePing;
  nombre: string;
  disponible: boolean;
  /** Presente sólo cuando `disponible` es `false`: distingue timeout de error de red/protocolo. */
  motivoFalla?: MotivoFalla;
  duracionMs: number;
  comprobadoEn: string;
}

export interface EstadoSistemaDto {
  estado: EstadoDisponibilidad;
  comprobadoEn: string;
  duracionMs: number;
  /** design D7: se agrega al mismo endpoint, sin exponer razón ni actor. */
  mantenimientoAsistente: MantenimientoAsistente;
}

interface RespuestaPing {
  status?: unknown;
}

interface RespuestaEstadoSistema {
  estado?: unknown;
  comprobadoEn: string;
  duracionMs: number;
  mantenimientoAsistente?: unknown;
}

function normalizarEstado(valor: unknown): EstadoDisponibilidad {
  return valor === "disponible" || valor === "no_disponible" ? valor : "desconocido";
}

function normalizarMantenimiento(valor: unknown): MantenimientoAsistente {
  return valor === "activo" || valor === "inactivo" ? valor : "desconocido";
}

/**
 * Una sonda por componente (design D12): pings HTTP de 5 s, medidos por el
 * cliente. `asistente` es una sonda más de esta lista (antes sólo se leía a
 * través de `/api/administracion/sistema/estado`); su estado de mantenimiento
 * viaja aparte, en `consultarEstadoSistema`.
 */
export async function comprobarPing(
  componente: (typeof COMPONENTES_PING)[number],
): Promise<ResultadoPing> {
  const inicio = performance.now();
  try {
    const { data } = await apiClient.get<RespuestaPing>(`/api/${componente.id}/ping`, {
      timeout: 5000,
    });
    const disponible = data.status === "ok";
    return {
      ...componente,
      disponible,
      motivoFalla: disponible ? undefined : "error",
      duracionMs: Math.round(performance.now() - inicio),
      comprobadoEn: new Date().toISOString(),
    };
  } catch (error) {
    const timeout = isAxiosError(error) && error.code === "ECONNABORTED";
    return {
      ...componente,
      disponible: false,
      motivoFalla: timeout ? "timeout" : "error",
      duracionMs: Math.round(performance.now() - inicio),
      comprobadoEn: new Date().toISOString(),
    };
  }
}

/** El chequeo de PostgreSQL, medido por el servidor, con el estado de mantenimiento del asistente. */
export async function consultarEstadoSistema(): Promise<EstadoSistemaDto> {
  const inicio = performance.now();
  try {
    const { data } = await apiClient.get<RespuestaEstadoSistema>(
      "/api/administracion/sistema/estado",
      { timeout: 5000 },
    );
    return {
      estado: normalizarEstado(data.estado),
      comprobadoEn: data.comprobadoEn,
      duracionMs: data.duracionMs,
      mantenimientoAsistente: normalizarMantenimiento(data.mantenimientoAsistente),
    };
  } catch {
    return {
      estado: "no_disponible",
      comprobadoEn: new Date().toISOString(),
      duracionMs: Math.round(performance.now() - inicio),
      mantenimientoAsistente: "desconocido",
    };
  }
}
