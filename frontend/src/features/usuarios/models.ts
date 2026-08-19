export type RolSistema = string;

export interface AsignacionRolUsuario {
  rolId: string;
  nombre: string;
  ambito: string;
  materiaId: string | null;
  carreraId: string | null;
}

export interface UsuarioMock {
  id: string;
  nombre: string;
  apellido: string;
  documento: string;
  legajo: string;
  cuil: string;
  fecha_nacimiento: string;
  telefono: string;
  upn: string;
  is_active: boolean;
  roles: RolSistema[];
  version?: number;
  asignaciones?: AsignacionRolUsuario[];
}

export function nombreCompleto(u: Pick<UsuarioMock, "apellido" | "nombre">): string {
  return `${u.apellido}, ${u.nombre}`;
}

export function normalizarTexto(s: string): string {
  return s.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
}
