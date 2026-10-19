import { apiClient } from "../../../shared/api/client";

/** Período lectivo informado por Guaraní; el año viene en su propio campo. */
export interface PeriodoLectivoGuarani {
  id: string;
  nombre: string;
  anio: number;
}

export interface ImportacionResumen {
  fecha: string;
  estado: "exitosa" | "fallida";
  causa: string | null;
  usuario: string | null;
}

export interface PeriodoImportado {
  anioAcademico: number;
  id: string;
  nombre: string;
  ultimaExitosa: ImportacionResumen | null;
  ultimoIntento: ImportacionResumen | null;
}

export interface HorarioComision {
  asignacionId: string;
  /** 1 = lunes … 7 = domingo. */
  dia: number;
  desde: string;
  hasta: string;
  aula: string | null;
}

export interface Comision {
  id: string;
  nombre: string;
  materia: { codigo: string; nombre: string; id: string | null };
  carreras: { codigo: string; nombre: string; id: string | null }[];
  docentes: { nombre: string; responsabilidad: string | null; personaId: string | null }[];
  horarios: HorarioComision[];
  modalidadCodigo: string | null;
  modalidad: string | null;
  ubicacion: string | null;
}

export interface ResultadoImportacion {
  id: string;
  anioAcademico: number;
  periodoLectivoId: string;
  periodoLectivoNombre: string;
  cantidad: number;
  docentesSinVincular: number;
  carrerasSinVincular: number;
}

const BASE = "/api/designaciones/comisiones";

export async function listarPeriodosImportados(): Promise<PeriodoImportado[]> {
  return (await apiClient.get<PeriodoImportado[]>(`${BASE}/periodos`)).data;
}

export async function listarComisiones(periodoLectivoId: string): Promise<Comision[]> {
  return (await apiClient.get<Comision[]>(BASE, { params: { periodoLectivoId } })).data;
}

export async function listarPeriodosGuarani(): Promise<PeriodoLectivoGuarani[]> {
  return (await apiClient.get<PeriodoLectivoGuarani[]>(`${BASE}/guarani/periodos-lectivos`)).data;
}

export async function importarComisiones(periodoLectivoId: string): Promise<ResultadoImportacion> {
  return (await apiClient.post<ResultadoImportacion>(`${BASE}/importaciones`, { periodoLectivoId }))
    .data;
}
