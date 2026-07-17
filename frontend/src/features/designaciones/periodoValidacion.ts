// ============================================================
// Validación del form de período de designación — LÓGICA PURA (sin React).
// Devuelve un mapa campo → mensaje; vacío ⇒ el período es válido.
// ============================================================
import type { PeriodoDesignacion } from "./types";

export type CampoPeriodo = "nombre" | "cargaHasta" | "impactoDesde" | "impactoHasta" | "activo";

export type ErroresValidacionPeriodo = Partial<Record<CampoPeriodo, string>>;

export interface DatosEditablesPeriodo {
  nombre: string;
  cargaDesde: string;
  cargaHasta: string;
  impactoDesde: string;
  impactoHasta: string;
  activo: boolean;
}

export interface ContextoValidacionPeriodo {
  /** Períodos ya existentes (para la regla de único período activo). */
  periodosExistentes: PeriodoDesignacion[];
  /** Id del período en edición, para no compararlo consigo mismo. */
  periodoActualId?: string;
}

/** Valida los datos del form y devuelve los errores por campo (vacío ⇒ válido). */
export function validarPeriodo(
  datos: DatosEditablesPeriodo,
  contexto: ContextoValidacionPeriodo,
): ErroresValidacionPeriodo {
  const errores: ErroresValidacionPeriodo = {};

  if (!datos.nombre.trim()) {
    errores.nombre = "El nombre del período es obligatorio.";
  }

  if (datos.cargaDesde && datos.cargaHasta && datos.cargaHasta < datos.cargaDesde) {
    errores.cargaHasta = "La fecha de carga hasta debe ser posterior o igual a la de carga desde.";
  }

  if (!datos.impactoDesde) {
    errores.impactoDesde = "La fecha de impacto desde es obligatoria.";
  }

  if (!datos.impactoHasta) {
    errores.impactoHasta = "La fecha de impacto hasta es obligatoria.";
  } else if (datos.impactoDesde && datos.impactoHasta < datos.impactoDesde) {
    errores.impactoHasta =
      "La fecha de impacto hasta debe ser posterior o igual a la de impacto desde.";
  }

  if (datos.activo) {
    const otroActivo = contexto.periodosExistentes.find(
      (p) => p.activo && p.id !== contexto.periodoActualId,
    );
    if (otroActivo) {
      errores.activo = `Ya existe un período activo: "${otroActivo.nombre}". Desactivalo primero.`;
    }
  }

  return errores;
}

/** True si no hay errores de validación. */
export function esPeriodoValido(errores: ErroresValidacionPeriodo): boolean {
  return Object.keys(errores).length === 0;
}
