export type RolSistema = string;

export interface AsignacionRolUsuario {
  id: string;
  rolId: string;
  codigo: string;
  nombre: string;
  ambito: string;
  materiaId: string | null;
  carreraId: string | null;
}

export interface RolCatalogoUsuario {
  id: string;
  codigo: string;
  nombre: string;
  ambito: string;
}

export interface UsuarioFormulario {
  nombre: string;
  apellido: string;
  documento: string;
  legajo: string;
  cuil: string;
  fecha_nacimiento: string;
  telefono: string;
  upn: string;
  membresias: Omit<AsignacionRolUsuario, "id" | "codigo" | "nombre" | "ambito">[];
  version?: number;
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
  membresias: AsignacionRolUsuario[];
  persona_id: string;
  perfilDocente: { esDocente: boolean; cantidadMaterias: number };
  version?: number;
}

export function nombreCompleto(u: Pick<UsuarioMock, "apellido" | "nombre">): string {
  return `${u.apellido}, ${u.nombre}`;
}

export function normalizarTexto(s: string): string {
  return s.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
}
