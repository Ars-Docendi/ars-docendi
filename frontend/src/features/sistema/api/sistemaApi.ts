import { apiClient } from "../../../shared/api/client";

export type EstadoComponente = "disponible" | "no_disponible" | "desconocido";

export interface ComprobacionComponente {
  id: string;
  nombre: string;
  estado: EstadoComponente;
  comprobadoEn: string;
  duracionMs: number;
}

export interface EstadoSistema {
  modulos: ComprobacionComponente[];
  baseDatos: ComprobacionComponente;
}

interface RespuestaPing {
  status?: string;
}

interface RespuestaEstadoBaseDatos {
  estado?: unknown;
  comprobadoEn: string;
  duracionMs: number;
}

function normalizarEstado(valor: unknown): EstadoComponente {
  return valor === "disponible" || valor === "no_disponible" ? valor : "desconocido";
}

const modulos = [
  { id: "aulas", nombre: "Aulas" },
  { id: "tareas", nombre: "Tareas" },
  { id: "designaciones", nombre: "Designaciones" },
  { id: "portal", nombre: "Portal" },
] as const;

async function comprobarModulo(modulo: (typeof modulos)[number]): Promise<ComprobacionComponente> {
  const inicio = performance.now();
  try {
    const { data } = await apiClient.get<RespuestaPing>(`/api/${modulo.id}/ping`, {
      timeout: 5000,
    });
    return {
      ...modulo,
      estado: data.status === "ok" ? "disponible" : "desconocido",
      comprobadoEn: new Date().toISOString(),
      duracionMs: Math.round(performance.now() - inicio),
    };
  } catch {
    return {
      ...modulo,
      estado: "no_disponible",
      comprobadoEn: new Date().toISOString(),
      duracionMs: Math.round(performance.now() - inicio),
    };
  }
}

async function comprobarBaseDatos(): Promise<ComprobacionComponente> {
  const inicio = performance.now();
  try {
    const { data } = await apiClient.get<RespuestaEstadoBaseDatos>(
      "/api/administracion/sistema/estado",
      { timeout: 5000 },
    );
    return {
      id: "postgresql",
      nombre: "PostgreSQL",
      estado: normalizarEstado(data.estado),
      comprobadoEn: data.comprobadoEn,
      duracionMs: data.duracionMs,
    };
  } catch {
    return {
      id: "postgresql",
      nombre: "PostgreSQL",
      estado: "no_disponible",
      comprobadoEn: new Date().toISOString(),
      duracionMs: Math.round(performance.now() - inicio),
    };
  }
}

/** Cada sonda es independiente: una falla no convierte el resto del tablero en una sola alarma. */
export async function consultarEstadoSistema(): Promise<EstadoSistema> {
  const [resultados, baseDatos] = await Promise.all([
    Promise.all(modulos.map(comprobarModulo)),
    comprobarBaseDatos(),
  ]);
  return { modulos: resultados, baseDatos };
}

export interface FiltrosAuditoria {
  pagina: number;
  tamanoPagina: number;
  desde?: string;
  hasta?: string;
  accion?: string;
  schema?: string;
  tabla?: string;
  cambiadoPor?: string;
  rowPk?: string;
}

export interface CambioAuditoria {
  campo: string;
  valorAnterior: string | null;
  valorNuevo: string | null;
  oculto: boolean;
}

export interface EventoAuditoria {
  id: number;
  schema: string;
  tabla: string;
  rowPk: string;
  accion: "INSERT" | "UPDATE" | "DELETE";
  cambiadoEn: string;
  cambiadoPor: string | null;
  requestId: string | null;
  columnasCambiadas: string[];
  cambios: CambioAuditoria[];
}

export interface PaginaAuditoria {
  elementos: EventoAuditoria[];
  pagina: number;
  tamanoPagina: number;
  total: number;
}

export async function listarAuditoria(filtros: FiltrosAuditoria): Promise<PaginaAuditoria> {
  const { data } = await apiClient.get<PaginaAuditoria>("/api/administracion/auditoria", {
    params: filtros,
  });
  return data;
}
