export interface AmbitoDesarrollo {
  id: string;
  codigo: string;
  nombre: string;
}

export interface RolDesarrollo {
  codigo: string;
  nombre: string;
  materias: AmbitoDesarrollo[];
  carreras: AmbitoDesarrollo[];
}

export interface IdentidadDesarrollo {
  usuarioId: string;
  nombreParaMostrar: string;
  upn: string;
  roles: RolDesarrollo[];
}
