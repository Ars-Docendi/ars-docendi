// ============================================================
// Catálogo mock de candidatos a Responsable/Autor de una tarea.
// Acotado a Tareas — NO importa de `features/usuarios` ni
// `features/docentes` (aislamiento de features). Los nombres
// coinciden con los usuarios mock de `shared/auth/dev/mockUsers.ts`
// para que el selector de rol de la topbar y el Responsable de una
// tarea sean consistentes en la demo.
// ============================================================
import type { PersonaCandidata } from "../types";

export const PERSONAS_CANDIDATAS: PersonaCandidata[] = [
  { nombre: "C. López", rol: "Docente" },
  { nombre: "G. Ruiz", rol: "Jefe de Cátedra" },
  { nombre: "M. Díaz", rol: "Coordinador" },
  { nombre: "L. Fernández", rol: "Secretaría" },
  { nombre: "R. Sosa", rol: "Decanato" },
  { nombre: "P. Gómez", rol: "Administración" },
];
