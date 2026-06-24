export interface MateriaMock {
  codigo: string; // 5 dígitos, ej: "03500"
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
}

export const ROLES_DOCENTE = ["Docente", "Jefe de Cátedra"] as const;
export type RolDocente = (typeof ROLES_DOCENTE)[number];

export interface AsignacionMateria {
  materia: MateriaMock;
  cargo: CargoDocente;
  horas: number;
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
}

export const MATERIAS_CATALOGO: MateriaMock[] = [
  { codigo: "03500", nombre: "Matemática Discreta" },
  { codigo: "00310", nombre: "Álgebra y Geometría Analítica" },
  { codigo: "00320", nombre: "Análisis Matemático I" },
  { codigo: "00330", nombre: "Análisis Matemático II" },
  { codigo: "04100", nombre: "Algoritmos y Estructuras de Datos" },
  { codigo: "04200", nombre: "Programación Orientada a Objetos" },
  { codigo: "04300", nombre: "Bases de Datos" },
  { codigo: "04400", nombre: "Sistemas Operativos" },
  { codigo: "05100", nombre: "Redes de Computadoras" },
  { codigo: "05200", nombre: "Ingeniería de Software" },
  { codigo: "05300", nombre: "Arquitectura de Computadoras" },
  { codigo: "06100", nombre: "Inteligencia Artificial" },
  { codigo: "06200", nombre: "Seguridad Informática" },
];

export const CARGOS_DOCENTES = [
  "Profesor Titular",
  "Profesor Asociado",
  "Profesor Adjunto",
  "Jefe de Trabajos Prácticos",
  "Ayudante de Primera",
  "Ayudante de Segunda",
] as const;

export type CargoDocente = (typeof CARGOS_DOCENTES)[number];

export const ABREV_CARGOS: Record<CargoDocente, string> = {
  "Profesor Titular": "Titular",
  "Profesor Asociado": "Asociado",
  "Profesor Adjunto": "Adjunto",
  "Jefe de Trabajos Prácticos": "JTP",
  "Ayudante de Primera": "Ay. 1ra",
  "Ayudante de Segunda": "Ay. 2da",
};

export const PERSONAS_SISTEMA: PersonaSistema[] = [
  {
    id: "p0000000-0000-4000-8000-000000000001",
    nombre: "Carla",
    apellido: "López",
    documento: "28341567",
    legajo: "0421",
    cuil: "27-28341567-3",
    fecha_nacimiento: "1980-03-14",
    telefono: "11-4523-8801",
    upn: "carla.lopez@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000002",
    nombre: "Gustavo",
    apellido: "Ruiz",
    documento: "22156789",
    legajo: "0115",
    cuil: "20-22156789-2",
    fecha_nacimiento: "1975-07-22",
    telefono: "11-6732-1145",
    upn: "gustavo.ruiz@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000003",
    nombre: "Marina",
    apellido: "Díaz",
    documento: "31089234",
    legajo: "0033",
    cuil: "27-31089234-8",
    fecha_nacimiento: "1985-11-05",
    telefono: "11-5548-9900",
    upn: "marina.diaz@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000004",
    nombre: "Paula",
    apellido: "Gómez",
    documento: "35678901",
    legajo: "0058",
    cuil: "27-35678901-9",
    fecha_nacimiento: "1992-06-11",
    telefono: "11-3324-6612",
    upn: "paula.gomez@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000005",
    nombre: "Sofía",
    apellido: "Peralta",
    documento: "38901234",
    legajo: "0387",
    cuil: "27-38901234-1",
    fecha_nacimiento: "1995-04-27",
    telefono: "11-2298-7754",
    upn: "sofia.peralta@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000006",
    nombre: "Federico",
    apellido: "Moreno",
    documento: "29876543",
    legajo: "0202",
    cuil: "20-29876543-1",
    fecha_nacimiento: "1982-08-15",
    telefono: "11-7711-2200",
    upn: "federico.moreno@unlam.edu.ar",
  },
  {
    id: "p0000000-0000-4000-8000-000000000007",
    nombre: "Natalia",
    apellido: "Castro",
    documento: "33445566",
    legajo: "0312",
    cuil: "27-33445566-5",
    fecha_nacimiento: "1988-01-20",
    telefono: "11-4455-7788",
    upn: "natalia.castro@unlam.edu.ar",
  },
];

export function nombreCompleto(d: Pick<DocenteMock, "apellido" | "nombre">): string {
  return `${d.apellido}, ${d.nombre}`;
}

export function normalizarTexto(s: string): string {
  return s.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
}

export const DOCENTES_INICIALES: DocenteMock[] = [
  {
    id: "d0000000-0000-4000-8000-000000000001",
    nombre: "Carlos",
    apellido: "Ramírez",
    documento: "22345678",
    legajo: "0101",
    cuil: "20-22345678-5",
    fecha_nacimiento: "1970-04-15",
    telefono: "11-4512-3340",
    upn: "carlos.ramirez@unlam.edu.ar",
    roles: ["Jefe de Cátedra"],
    asignaciones: [
      {
        materia: { codigo: "03500", nombre: "Matemática Discreta" },
        cargo: "Profesor Titular",
        horas: 6,
      },
      {
        materia: { codigo: "00320", nombre: "Análisis Matemático I" },
        cargo: "Profesor Titular",
        horas: 4,
      },
    ],
    is_active: true,
  },
  {
    id: "d0000000-0000-4000-8000-000000000002",
    nombre: "Silvia",
    apellido: "Méndez",
    documento: "28901234",
    legajo: "0054",
    cuil: "27-28901234-7",
    fecha_nacimiento: "1980-09-22",
    telefono: "11-6710-2290",
    upn: "silvia.mendez@unlam.edu.ar",
    roles: ["Docente"],
    asignaciones: [
      {
        materia: { codigo: "04100", nombre: "Algoritmos y Estructuras de Datos" },
        cargo: "Profesor Adjunto",
        horas: 8,
      },
    ],
    is_active: true,
  },
  {
    id: "d0000000-0000-4000-8000-000000000003",
    nombre: "Marcelo",
    apellido: "Torres",
    documento: "31234567",
    legajo: "0078",
    cuil: "20-31234567-9",
    fecha_nacimiento: "1983-02-10",
    telefono: "11-5533-8811",
    upn: "marcelo.torres@unlam.edu.ar",
    roles: ["Docente"],
    asignaciones: [
      {
        materia: { codigo: "04200", nombre: "Programación Orientada a Objetos" },
        cargo: "Jefe de Trabajos Prácticos",
        horas: 6,
      },
      {
        materia: { codigo: "04300", nombre: "Bases de Datos" },
        cargo: "Ayudante de Primera",
        horas: 4,
      },
    ],
    is_active: true,
  },
  {
    id: "d0000000-0000-4000-8000-000000000004",
    nombre: "Ana",
    apellido: "López",
    documento: "25678901",
    legajo: "0023",
    cuil: "27-25678901-1",
    fecha_nacimiento: "1975-07-30",
    telefono: "11-4499-6655",
    upn: "ana.lopez@unlam.edu.ar",
    roles: ["Jefe de Cátedra"],
    asignaciones: [
      {
        materia: { codigo: "05100", nombre: "Redes de Computadoras" },
        cargo: "Profesor Titular",
        horas: 6,
      },
      {
        materia: { codigo: "06200", nombre: "Seguridad Informática" },
        cargo: "Profesor Asociado",
        horas: 4,
      },
    ],
    is_active: true,
  },
  {
    id: "d0000000-0000-4000-8000-000000000005",
    nombre: "Roberto",
    apellido: "Fernández",
    documento: "19876543",
    legajo: "0012",
    cuil: "20-19876543-3",
    fecha_nacimiento: "1965-11-08",
    telefono: "11-4001-7788",
    upn: "roberto.fernandez@unlam.edu.ar",
    roles: ["Docente"],
    asignaciones: [
      {
        materia: { codigo: "00310", nombre: "Álgebra y Geometría Analítica" },
        cargo: "Profesor Adjunto",
        horas: 8,
      },
      {
        materia: { codigo: "00330", nombre: "Análisis Matemático II" },
        cargo: "Jefe de Trabajos Prácticos",
        horas: 6,
      },
    ],
    is_active: false,
  },
  {
    id: "d0000000-0000-4000-8000-000000000006",
    nombre: "Valeria",
    apellido: "Sosa",
    documento: "35123456",
    legajo: "0145",
    cuil: "27-35123456-2",
    fecha_nacimiento: "1990-03-25",
    telefono: "11-2255-4499",
    upn: "valeria.sosa@unlam.edu.ar",
    roles: ["Docente"],
    asignaciones: [
      {
        materia: { codigo: "04400", nombre: "Sistemas Operativos" },
        cargo: "Ayudante de Primera",
        horas: 4,
      },
    ],
    is_active: true,
  },
  {
    id: "d0000000-0000-4000-8000-000000000007",
    nombre: "Diego",
    apellido: "García",
    documento: "27890123",
    legajo: "0067",
    cuil: "20-27890123-8",
    fecha_nacimiento: "1978-12-14",
    telefono: "11-7766-3322",
    upn: "diego.garcia@unlam.edu.ar",
    roles: ["Jefe de Cátedra"],
    asignaciones: [
      {
        materia: { codigo: "05200", nombre: "Ingeniería de Software" },
        cargo: "Profesor Titular",
        horas: 6,
      },
      {
        materia: { codigo: "06100", nombre: "Inteligencia Artificial" },
        cargo: "Profesor Adjunto",
        horas: 4,
      },
    ],
    is_active: true,
  },
  {
    id: "d0000000-0000-4000-8000-000000000008",
    nombre: "Luciana",
    apellido: "Paz",
    documento: "38456789",
    legajo: "0198",
    cuil: "27-38456789-0",
    fecha_nacimiento: "1993-06-18",
    telefono: "11-3344-5566",
    upn: "luciana.paz@unlam.edu.ar",
    roles: ["Docente"],
    asignaciones: [
      {
        materia: { codigo: "05300", nombre: "Arquitectura de Computadoras" },
        cargo: "Ayudante de Segunda",
        horas: 4,
      },
      {
        materia: { codigo: "04100", nombre: "Algoritmos y Estructuras de Datos" },
        cargo: "Ayudante de Primera",
        horas: 4,
      },
    ],
    is_active: true,
  },
  {
    id: "d0000000-0000-4000-8000-000000000009",
    nombre: "Gustavo",
    apellido: "Ruiz",
    documento: "22156789",
    legajo: "0115",
    cuil: "20-22156789-2",
    fecha_nacimiento: "1975-07-22",
    telefono: "11-6732-1145",
    upn: "gustavo.ruiz@unlam.edu.ar",
    roles: ["Docente", "Jefe de Cátedra"],
    asignaciones: [
      {
        materia: { codigo: "05200", nombre: "Ingeniería de Software" },
        cargo: "Profesor Titular",
        horas: 6,
      },
    ],
    is_active: true,
  },
];

export function agregarDocente(
  lista: DocenteMock[],
  nuevo: Omit<DocenteMock, "id" | "is_active">,
): DocenteMock[] {
  return [...lista, { ...nuevo, id: crypto.randomUUID(), is_active: true }];
}

export function editarDocente(
  lista: DocenteMock[],
  id: string,
  datos: Omit<DocenteMock, "id" | "is_active">,
): DocenteMock[] {
  return lista.map((d) => (d.id === id ? { ...d, ...datos } : d));
}

export function desactivarDocente(lista: DocenteMock[], id: string): DocenteMock[] {
  return lista.map((d) => (d.id === id ? { ...d, is_active: false } : d));
}

export function activarDocente(lista: DocenteMock[], id: string): DocenteMock[] {
  return lista.map((d) => (d.id === id ? { ...d, is_active: true } : d));
}
