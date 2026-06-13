export type RolSistema =
  | "Docente"
  | "Jefe de Cátedra"
  | "Coordinador de Carrera"
  | "Secretaría Académica"
  | "Decanato"
  | "Administrativo";

export const ROLES_SISTEMA: RolSistema[] = [
  "Docente",
  "Jefe de Cátedra",
  "Coordinador de Carrera",
  "Secretaría Académica",
  "Decanato",
  "Administrativo",
];

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
}

export function nombreCompleto(u: Pick<UsuarioMock, "apellido" | "nombre">): string {
  return `${u.apellido}, ${u.nombre}`;
}

export function normalizarTexto(s: string): string {
  return s.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
}

export const USUARIOS_INICIALES: UsuarioMock[] = [
  {
    id: "a0000000-0000-4000-8000-000000000001",
    nombre: "Carla",
    apellido: "López",
    documento: "28341567",
    legajo: "0421",
    cuil: "27-28341567-3",
    fecha_nacimiento: "1980-03-14",
    telefono: "11-4523-8801",
    upn: "carla.lopez@unlam.edu.ar",
    is_active: true,
    roles: ["Docente"],
  },
  {
    id: "a0000000-0000-4000-8000-000000000002",
    nombre: "Gustavo",
    apellido: "Ruiz",
    documento: "22156789",
    legajo: "0115",
    cuil: "20-22156789-2",
    fecha_nacimiento: "1975-07-22",
    telefono: "11-6732-1145",
    upn: "gustavo.ruiz@unlam.edu.ar",
    is_active: true,
    roles: ["Jefe de Cátedra", "Docente"],
  },
  {
    id: "a0000000-0000-4000-8000-000000000003",
    nombre: "Marina",
    apellido: "Díaz",
    documento: "31089234",
    legajo: "0033",
    cuil: "27-31089234-8",
    fecha_nacimiento: "1985-11-05",
    telefono: "11-5548-9900",
    upn: "marina.diaz@unlam.edu.ar",
    is_active: true,
    roles: ["Coordinador de Carrera"],
  },
  {
    id: "a0000000-0000-4000-8000-000000000004",
    nombre: "Lucía",
    apellido: "Fernández",
    documento: "19876543",
    legajo: "0007",
    cuil: "27-19876543-6",
    fecha_nacimiento: "1970-01-30",
    telefono: "11-4789-2233",
    upn: "secretaria.academica@unlam.edu.ar",
    is_active: true,
    roles: ["Secretaría Académica", "Administrativo"],
  },
  {
    id: "a0000000-0000-4000-8000-000000000005",
    nombre: "Roberto",
    apellido: "Sosa",
    documento: "15432109",
    legajo: "0002",
    cuil: "20-15432109-4",
    fecha_nacimiento: "1965-09-18",
    telefono: "11-4001-5577",
    upn: "decanato@unlam.edu.ar",
    is_active: true,
    roles: ["Decanato"],
  },
  {
    id: "a0000000-0000-4000-8000-000000000006",
    nombre: "Paula",
    apellido: "Gómez",
    documento: "35678901",
    legajo: "0058",
    cuil: "27-35678901-9",
    fecha_nacimiento: "1992-06-11",
    telefono: "11-3324-6612",
    upn: "admin.aulas@unlam.edu.ar",
    is_active: true,
    roles: ["Administrativo"],
  },
  {
    id: "a0000000-0000-4000-8000-000000000007",
    nombre: "Ernesto",
    apellido: "Vidal",
    documento: "26543210",
    legajo: "0299",
    cuil: "20-26543210-7",
    fecha_nacimiento: "1978-12-03",
    telefono: "11-7865-4430",
    upn: "ernesto.vidal@unlam.edu.ar",
    is_active: false,
    roles: ["Docente"],
  },
  {
    id: "a0000000-0000-4000-8000-000000000008",
    nombre: "Sofía",
    apellido: "Peralta",
    documento: "38901234",
    legajo: "0387",
    cuil: "27-38901234-1",
    fecha_nacimiento: "1995-04-27",
    telefono: "11-2298-7754",
    upn: "sofia.peralta@unlam.edu.ar",
    is_active: true,
    roles: ["Docente", "Coordinador de Carrera"],
  },
];

export function agregarUsuario(
  lista: UsuarioMock[],
  nuevo: Omit<UsuarioMock, "id" | "is_active">,
): UsuarioMock[] {
  const usuario: UsuarioMock = {
    ...nuevo,
    id: crypto.randomUUID(),
    is_active: true,
  };
  return [...lista, usuario];
}

export function editarUsuario(
  lista: UsuarioMock[],
  id: string,
  datos: Omit<UsuarioMock, "id" | "is_active">,
): UsuarioMock[] {
  return lista.map((u) => (u.id === id ? { ...u, ...datos } : u));
}

export function desactivarUsuario(lista: UsuarioMock[], id: string): UsuarioMock[] {
  return lista.map((u) => (u.id === id ? { ...u, is_active: false } : u));
}

export function activarUsuario(lista: UsuarioMock[], id: string): UsuarioMock[] {
  return lista.map((u) => (u.id === id ? { ...u, is_active: true } : u));
}
