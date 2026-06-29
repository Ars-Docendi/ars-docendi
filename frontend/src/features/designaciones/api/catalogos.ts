// ============================================================
// Catálogos del módulo (mock del prototipo frontend-only).
// Alimentan los selects del form de pedido: materias de la cátedra,
// cargos/dedicaciones del régimen docente y los docentes ya existentes
// (con su designación vigente) para las novedades sobre docentes
// existentes. En el real provendrían de la API Guaraní / módulo Portal.
// ============================================================
import type { Cargo, Dedicacion, DocenteExistente } from "../types";

/** Cargos del régimen docente, de mayor a menor jerarquía. */
export const CARGOS: Cargo[] = ["Titular", "Adjunto", "JTP", "Ayudante"];

/** Dedicaciones (categorías) del régimen docente. */
export const DEDICACIONES: Dedicacion[] = [
  "Categoría 1",
  "Categoría 2",
  "Categoría 3",
  "Categoría 4",
  "Categoría 5",
  "Categoría 6",
];

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
 * que operan sobre un docente existente (Sin novedad / Baja / Cambio).
 */
export const DOCENTES_EXISTENTES: DocenteExistente[] = [
  {
    dni: "28341567",
    nombre: "Lucía Fernández",
    antiguedad: 8,
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    materiaActual: "Programación I",
  },
  {
    dni: "27345678",
    nombre: "Laura Giménez",
    antiguedad: 12,
    cargoActual: "Titular",
    dedicacionActual: "Categoría 2",
    materiaActual: "Ingeniería de Software",
  },
  {
    dni: "30987654",
    nombre: "Diego Morales",
    antiguedad: 7,
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    materiaActual: "Ingeniería de Software",
  },
  {
    dni: "33112233",
    nombre: "Sofía Romano",
    antiguedad: 4,
    cargoActual: "Ayudante",
    dedicacionActual: "Categoría 5",
    materiaActual: "Algoritmos y Estructuras de Datos",
  },
  {
    dni: "28776655",
    nombre: "Valeria Suárez",
    antiguedad: 9,
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 4",
    materiaActual: "Ingeniería de Software",
  },
  {
    dni: "31445566",
    nombre: "Pablo Herrera",
    antiguedad: 6,
    cargoActual: "JTP",
    dedicacionActual: "Categoría 4",
    materiaActual: "Algoritmos y Estructuras de Datos",
  },
  {
    dni: "27660011",
    nombre: "Gabriel Núñez",
    antiguedad: 11,
    cargoActual: "Adjunto",
    dedicacionActual: "Categoría 3",
    materiaActual: "Ingeniería de Software",
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
