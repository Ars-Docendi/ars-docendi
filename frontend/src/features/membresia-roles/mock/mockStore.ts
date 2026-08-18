export interface PermisoMock {
  id: string;
  nombre: string;
  desc: string;
}

export const PERMISOS_INICIALES: PermisoMock[] = [
  {
    id: "p001",
    nombre: "Ver designaciones",
    desc: "Consultar el estado y detalle de designaciones sin modificarlas.",
  },
  {
    id: "p002",
    nombre: "Gestionar designaciones",
    desc: "Crear y editar proyectos docentes e iniciar el flujo de designación.",
  },
  {
    id: "p003",
    nombre: "Aprobar designaciones — Coordinación",
    desc: "Aprobar o rechazar designaciones en la instancia de coordinación de carrera.",
  },
  {
    id: "p004",
    nombre: "Aprobar designaciones — Secretaría",
    desc: "Aprobar o rechazar designaciones en la instancia de secretaría académica.",
  },
  {
    id: "p005",
    nombre: "Aprobar designaciones — Decanato",
    desc: "Aprobar o rechazar designaciones en la instancia final del decanato.",
  },
  {
    id: "p006",
    nombre: "Ver reservas de aulas",
    desc: "Consultar el calendario de reservas de aulas y laboratorios.",
  },
  {
    id: "p007",
    nombre: "Gestionar reservas de aulas",
    desc: "Solicitar y asignar aulas o laboratorios para mesas de examen.",
  },
  {
    id: "p008",
    nombre: "Aprobar reservas de aulas",
    desc: "Confirmar o rechazar pedidos de reserva realizados por administrativos.",
  },
  {
    id: "p009",
    nombre: "Ver usuarios",
    desc: "Consultar el listado de usuarios registrados en el sistema.",
  },
  {
    id: "p010",
    nombre: "Administrar usuarios",
    desc: "Crear, editar, activar y desactivar cuentas de usuario del sistema.",
  },
  { id: "p011", nombre: "Ver roles", desc: "Consultar el listado de roles y sus descripciones." },
  { id: "p012", nombre: "Administrar roles", desc: "Crear y modificar roles del sistema." },
  {
    id: "p013",
    nombre: "Gestionar membresía de roles",
    desc: "Asignar y revocar permisos a cada rol.",
  },
  {
    id: "p014",
    nombre: "Administrar períodos",
    desc: "Gestionar los períodos académicos habilitados para designaciones y reservas.",
  },
  {
    id: "p015",
    nombre: "Parametrizar sistema",
    desc: "Configurar parámetros generales (umbrales, textos, fechas de corte).",
  },
  {
    id: "p016",
    nombre: "Ver tareas",
    desc: "Consultar el tablero de tareas internas del departamento.",
  },
  {
    id: "p017",
    nombre: "Gestionar tareas",
    desc: "Crear, editar, asignar y cerrar tareas internas del departamento.",
  },
  {
    id: "p018",
    nombre: "Ver portal personal",
    desc: "Acceder al portal propio con datos personales y horas disponibles.",
  },
  {
    id: "p019",
    nombre: "Editar portal personal",
    desc: "Actualizar datos personales, horas disponibles y áreas de experticia.",
  },
  {
    id: "p020",
    nombre: "Ver reportes globales",
    desc: "Acceder a reportes consolidados de designaciones, aulas y actividad docente.",
  },
];

export type MapaMembresias = Record<string, string[]>;

export const MEMBRESIAS_INICIALES: MapaMembresias = {
  "a1000000-0000-4000-8000-000000000001": ["p018", "p019"],
  "a1000000-0000-4000-8000-000000000002": ["p001", "p002", "p006", "p018", "p019"],
  "a1000000-0000-4000-8000-000000000003": ["p001", "p003", "p006", "p018", "p019", "p020"],
  "a1000000-0000-4000-8000-000000000004": [
    "p001",
    "p004",
    "p006",
    "p007",
    "p008",
    "p009",
    "p010",
    "p011",
    "p012",
    "p013",
    "p014",
    "p015",
    "p016",
    "p017",
    "p018",
    "p019",
    "p020",
  ],
  "a1000000-0000-4000-8000-000000000005": ["p001", "p005", "p018", "p019", "p020"],
  "a1000000-0000-4000-8000-000000000006": [
    "p001",
    "p006",
    "p007",
    "p008",
    "p009",
    "p010",
    "p016",
    "p017",
    "p018",
    "p019",
  ],
  "a1000000-0000-4000-8000-000000000007": [
    "p001",
    "p002",
    "p003",
    "p004",
    "p005",
    "p006",
    "p007",
    "p008",
    "p009",
    "p010",
    "p011",
    "p012",
    "p013",
    "p014",
    "p015",
    "p016",
    "p017",
    "p018",
    "p019",
    "p020",
  ],
};

export function actualizarMembresia(
  mapa: MapaMembresias,
  rolId: string,
  permisosIds: string[],
): MapaMembresias {
  return { ...mapa, [rolId]: permisosIds };
}
