// ============================================================
// Catálogos del módulo (mock del prototipo frontend-only).
// Alimentan los selects del form de pedido: materias de la cátedra,
// cargos/dedicaciones del régimen docente y los docentes ya existentes
// (con su designación vigente) para las novedades sobre docentes
// existentes. En el real provendrían de la API Guaraní / módulo Portal.
// ============================================================
import type {
  Cargo,
  Dedicacion,
  DepartamentoAgenteExterno,
  DocenteExistente,
  TipoBaja,
} from "../types";

/** Cargos del régimen docente, de mayor a menor jerarquía. */
export const CARGOS: Cargo[] = ["Titular", "Adjunto", "JTP", "Ayudante"];

/** Dedicaciones (categorías) del régimen docente. */
export const DEDICACIONES: Dedicacion[] = [
  "Categoría 0",
  "Categoría 1",
  "Categoría 2",
  "Categoría 3",
  "Categoría 4",
  "Categoría 5",
  "Categoría 6",
];

/** Tipos de baja del docente (enum cerrado; "Otro" exige detalle en texto libre). */
export const TIPOS_BAJA: TipoBaja[] = ["Renuncia", "Jubilación", "Otro"];

/** Departamentos/dependencias a cargo de un docente marcado como agente externo (catálogo cerrado). */
export const DEPARTAMENTOS_AGENTE_EXTERNO: DepartamentoAgenteExterno[] = [
  "Departamento de Arquitectura",
  "Departamento de Salud",
  "Departamento de Derecho",
  "Departamento de Económicas",
  "Departamento de Humanidades",
  "Departamento de Odontología",
  "Secretaría Académica",
];

/**
 * Índice numérico de una dedicación ("Categoría 3" → 3). La escala es
 * descendente: 0 es la de mayor jerarquía, 6 la de menor — en Cambio, una
 * dedicación solicitada "mejor" que la actual tiene índice estrictamente menor.
 */
export function indiceDedicacion(dedicacion: Dedicacion): number {
  return Number(dedicacion.replace("Categoría ", ""));
}

/** Materias asociables a un pedido de designación. */
export const MATERIAS: string[] = [
  "Ingeniería de Software",
  "Algoritmos y Estructuras de Datos",
  "Programación I",
  "Programación II",
  "Bases de Datos",
  "Sistemas Operativos",
  "Matemática Discreta",
  "Física I",
];

/**
 * Docentes con designación vigente, disponibles para las novedades
 * que operan sobre un docente existente (Baja / Cambio).
 */
export const DOCENTES_EXISTENTES: DocenteExistente[] = [
  {
    dni: "28341567",
    nombre: "Lucía Fernández",
    legajo: "1001",
    antiguedad: 8,
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    materiasActuales: [
      { materia: "Programación I", horas: 6 },
      { materia: "Ingeniería de Software", horas: 4 },
    ],
    horasInvestigacionActuales: 2,
    horasExternasActuales: 0,
  },
  {
    dni: "27345678",
    nombre: "Laura Giménez",
    legajo: "1002",
    antiguedad: 12,
    cargoActual: "Titular",
    dedicacionActual: "Categoría 2",
    materiasActuales: [{ materia: "Ingeniería de Software", horas: 8 }],
    horasInvestigacionActuales: 4,
    horasExternasActuales: 0,
  },
  {
    dni: "30987654",
    nombre: "Diego Morales",
    legajo: "1003",
    antiguedad: 7,
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    materiasActuales: [{ materia: "Ingeniería de Software", horas: 6 }],
    horasInvestigacionActuales: 0,
    horasExternasActuales: 2,
  },
  {
    dni: "33112233",
    nombre: "Sofía Romano",
    legajo: "1004",
    antiguedad: 4,
    cargoActual: "Ayudante",
    dedicacionActual: "Categoría 5",
    materiasActuales: [{ materia: "Algoritmos y Estructuras de Datos", horas: 4 }],
    horasInvestigacionActuales: 0,
    horasExternasActuales: 0,
  },
  {
    dni: "28776655",
    nombre: "Valeria Suárez",
    legajo: "1005",
    antiguedad: 9,
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 4",
    materiasActuales: [{ materia: "Ingeniería de Software", horas: 6 }],
    horasInvestigacionActuales: 3,
    horasExternasActuales: 0,
  },
  {
    dni: "31445566",
    nombre: "Pablo Herrera",
    legajo: "1006",
    antiguedad: 6,
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    materiasActuales: [{ materia: "Algoritmos y Estructuras de Datos", horas: 6 }],
    horasInvestigacionActuales: 0,
    horasExternasActuales: 0,
  },
  {
    dni: "27660011",
    nombre: "Gabriel Núñez",
    legajo: "1007",
    antiguedad: 11,
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    materiasActuales: [{ materia: "Ingeniería de Software", horas: 6 }],
    horasInvestigacionActuales: 0,
    horasExternasActuales: 4,
  },
];

/** Formatea un DNI numérico con separadores de miles ("28341567" → "28.341.567"). */
export function formatearDni(dni: string): string {
  const limpio = dni.replace(/\D/g, "");
  if (!limpio) return dni;
  return limpio.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
}

/** Busca un docente existente por DNI (normalizando separadores). */
export function buscarDocenteExistente(dni: string): DocenteExistente | undefined {
  const limpio = dni.replace(/\D/g, "");
  return DOCENTES_EXISTENTES.find((docente) => docente.dni === limpio);
}
