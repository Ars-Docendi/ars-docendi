export interface MateriaMock {
  id: string;
  codigo: string;
  nombre: string;
}
export interface PersonaSistema {
  id: string;
  nombre: string;
  apellido: string;
  documento: string;
  legajo: string;
  cuil: string;
  fecha_nacimiento: string;
  telefono: string;
  upn: string;
  version?: number;
}
export type RolDocente = string;
export type CargoDocente = string;
export interface AsignacionMateria {
  id?: string;
  materia: MateriaMock;
  cargo: CargoDocente;
  cargoId?: string;
  cargoAbreviatura?: string;
  horas: number;
  dedicacion?: string | null;
}
export interface DocenteMock {
  id: string;
  nombre: string;
  apellido: string;
  documento: string;
  legajo: string;
  cuil: string;
  fecha_nacimiento: string;
  telefono: string;
  upn: string;
  roles: RolDocente[];
  asignaciones: AsignacionMateria[];
  is_active: boolean;
  version?: number;
  persona_id?: string;
}
export function nombreCompleto(d: Pick<DocenteMock, "apellido" | "nombre">): string {
  return `${d.apellido}, ${d.nombre}`;
}
export function normalizarTexto(s: string): string {
  return s.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
}
