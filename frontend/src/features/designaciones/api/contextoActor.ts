// ============================================================
// Seam del ámbito del actor (carrera / cátedra).
// Parte de la capa `api/` porque el ámbito real vendrá del backend.
// ============================================================
import type { ActorContexto, Rol } from "../types";

// TODO(backend): el ámbito (carrera/cátedra) del actor vendrá de los claims del
//   usuario (Azure AD) o de un endpoint de identidad — SCRUM-6/7. Hoy es un mapa
//   mock por rol alineado al seed de pedidos. Mantener la firma de construirActorContexto.
const AMBITO_POR_ROL: Partial<Record<Rol, { carrera?: string; catedra?: string }>> = {
  "Jefe de Cátedra": { carrera: "Ingeniería en Informática", catedra: "Ingeniería de Software" },
  Coordinador: { carrera: "Ingeniería en Informática" },
};

export function construirActorContexto(rol: Rol, nombre: string): ActorContexto {
  const ambito = AMBITO_POR_ROL[rol] ?? {};
  return { rol, nombre, carrera: ambito.carrera, catedra: ambito.catedra };
}
