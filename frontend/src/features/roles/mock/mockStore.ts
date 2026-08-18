export interface RolMock {
  id: string;
  codigo: string;
  nombre: string;
  descripcion: string;
  scope: ScopeRol;
  es_sistema: boolean;
}

export const SCOPES_ROL = ["global", "materia", "carrera"] as const;
export type ScopeRol = (typeof SCOPES_ROL)[number];

export const ETIQUETAS_SCOPE: Record<ScopeRol, string> = {
  global: "Global",
  materia: "Materia",
  carrera: "Carrera",
};

export type DatosRolNuevo = Pick<RolMock, "nombre" | "descripcion" | "scope">;
export type DatosRolEditables = Pick<RolMock, "nombre" | "descripcion" | "scope">;

export function normalizarTexto(s: string): string {
  return s.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
}

export const ROLES_INICIALES: RolMock[] = [
  {
    id: "a1000000-0000-4000-8000-000000000001",
    codigo: "docente",
    nombre: "Docente",
    descripcion: "Acceso básico para docentes: reserva de aulas y portal personal.",
    scope: "materia",
    es_sistema: true,
  },
  {
    id: "a1000000-0000-4000-8000-000000000002",
    codigo: "jefe_catedra",
    nombre: "Jefe de Cátedra",
    descripcion: "Gestiona el proyecto docente de su cátedra y puede generar designaciones.",
    scope: "materia",
    es_sistema: true,
  },
  {
    id: "a1000000-0000-4000-8000-000000000003",
    codigo: "coordinador_carrera",
    nombre: "Coordinador de Carrera",
    descripcion: "Aprueba o rechaza novedades de su carrera y supervisa designaciones.",
    scope: "carrera",
    es_sistema: true,
  },
  {
    id: "a1000000-0000-4000-8000-000000000004",
    codigo: "secretaria",
    nombre: "Secretaría Académica",
    descripcion: "Administra todo el departamento: usuarios, roles, aulas y designaciones.",
    scope: "global",
    es_sistema: true,
  },
  {
    id: "a1000000-0000-4000-8000-000000000005",
    codigo: "decanato",
    nombre: "Decanato",
    descripcion: "Aprobación final de designaciones y supervisión general del sistema.",
    scope: "global",
    es_sistema: true,
  },
  {
    id: "a1000000-0000-4000-8000-000000000006",
    codigo: "administrativo",
    nombre: "Administrativo",
    descripcion: "Gestión administrativa: reservas de aulas, configuración y usuarios.",
    scope: "global",
    es_sistema: true,
  },
  {
    id: "a1000000-0000-4000-8000-000000000007",
    codigo: "sys_admin",
    nombre: "Administrador de Sistemas",
    descripcion: "Administra la configuración técnica y el acceso global al sistema.",
    scope: "global",
    es_sistema: true,
  },
];

export function generarCodigoRol(nombre: string): string {
  return normalizarTexto(nombre)
    .trim()
    .replace(/[^a-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "");
}

export function editarRol(lista: RolMock[], id: string, datos: DatosRolEditables): RolMock[] {
  return lista.map((r) => (r.id === id ? { ...r, ...datos } : r));
}
