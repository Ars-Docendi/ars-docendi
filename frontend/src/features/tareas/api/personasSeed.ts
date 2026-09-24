// ============================================================
// Catálogo mock de candidatos a Responsable/Autor de una tarea.
// Acotado a Tareas — NO importa de `features/usuarios` ni
// `features/docentes` (aislamiento de features). Los nombres de
// rol coinciden EXACTO con los que devuelve `GET /api/desarrollo/
// identidades` (sembrados por `Modules.Identity`, ver
// `shared/auth/dev/useIdentidadesDesarrollo.ts`) — no son
// abreviaturas propias de Tareas, para que `actor.rol` (derivado de
// `useCurrentUser`) matchee contra estos mismos valores en
// `maquinaEstadosTarea.ts`/`maquinaEstadosProyecto.ts`.
// ============================================================
import type { PersonaCandidata } from "../types";

export const PERSONAS_CANDIDATAS: PersonaCandidata[] = [
  { nombre: "C. López", rol: "Docente" },
  { nombre: "G. Ruiz", rol: "Jefe de Cátedra" },
  { nombre: "M. Díaz", rol: "Coordinador de Carrera" },
  { nombre: "L. Fernández", rol: "Secretaría Académica" },
  { nombre: "R. Sosa", rol: "Decanato" },
  { nombre: "P. Gómez", rol: "Administrativo" },
];
