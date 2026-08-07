// ============================================================
// Seam del ámbito del actor (carrera / cátedra).
// Parte de la capa `api/` porque el ámbito real vendrá del backend.
// ============================================================
import type { ActorContexto, Rol } from "../types";

// TODO(backend): el ámbito (carrera/cátedra) del actor vendrá de los claims del
//   usuario (Azure AD) o de un endpoint de identidad — SCRUM-6/7. Hoy es un mapa
//   mock por rol alineado al seed de pedidos. Mantener la firma de construirActorContexto.
const AMBITO_POR_ROL: Partial<Record<Rol, { carrera?: string; catedras?: string[] }>> = {
  // El rol tiene ámbito de materia y se puede otorgar varias veces al mismo
  // usuario, así que un JC puede estar a cargo de más de una cátedra. Estas son
  // las de Informática que aparecen en el seed; "Física I" queda afuera a
  // propósito: es de Ingeniería Industrial y no debe verla [BR-009].
  "Jefe de Cátedra": {
    carrera: "Ingeniería en Informática",
    catedras: [
      "Ingeniería de Software",
      "Algoritmos y Estructuras de Datos",
      "Bases de Datos",
      "Programación I",
      "Sistemas Operativos",
      "Matemática Discreta",
      "Redes de Computadoras",
    ],
  },
  Coordinador: { carrera: "Ingeniería en Informática" },
  // Depto-wide (sin carrera): ven y actúan sobre todo el departamento [BR-009].
  Secretaría: {},
  Decanato: {},
  Administración: {},
};

export function construirActorContexto(rol: Rol, nombre: string): ActorContexto {
  const ambito = AMBITO_POR_ROL[rol] ?? {};
  return { rol, nombre, carrera: ambito.carrera, catedras: ambito.catedras };
}
