export interface RolMock {
  id: string;
  nombre: string;
  descripcion: string;
}

export function normalizarTexto(s: string): string {
  return s.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
}

export const ROLES_INICIALES: RolMock[] = [
  {
    id: "r0000000-0000-4000-8000-000000000001",
    nombre: "Docente",
    descripcion: "Acceso básico para docentes: reserva de aulas y portal personal.",
  },
  {
    id: "r0000000-0000-4000-8000-000000000002",
    nombre: "Jefe de Cátedra",
    descripcion: "Gestiona el proyecto docente de su cátedra y puede generar designaciones.",
  },
  {
    id: "r0000000-0000-4000-8000-000000000003",
    nombre: "Coordinador",
    descripcion: "Aprueba o rechaza novedades de su carrera y supervisa designaciones.",
  },
  {
    id: "r0000000-0000-4000-8000-000000000004",
    nombre: "Secretaría",
    descripcion: "Administra todo el departamento: usuarios, roles, aulas y designaciones.",
  },
  {
    id: "r0000000-0000-4000-8000-000000000005",
    nombre: "Decanato",
    descripcion: "Aprobación final de designaciones y supervisión general del sistema.",
  },
  {
    id: "r0000000-0000-4000-8000-000000000006",
    nombre: "Administración",
    descripcion: "Gestión administrativa: reservas de aulas, configuración y usuarios.",
  },
];

export function agregarRol(lista: RolMock[], nuevo: Omit<RolMock, "id">): RolMock[] {
  return [...lista, { ...nuevo, id: crypto.randomUUID() }];
}

export function editarRol(lista: RolMock[], id: string, datos: Omit<RolMock, "id">): RolMock[] {
  return lista.map((r) => (r.id === id ? { ...r, ...datos } : r));
}
