import { apiClient } from "../../../shared/api/client";
import type { Dedicacion, DocenteExistente, PeriodoDesignacion } from "../types";

export interface CatalogosDesignaciones {
  periodoActivo: PeriodoDesignacion | null;
  periodos: PeriodoDesignacion[];
  materias: { id: string; codigo: string; nombre: string; carreraId: string }[];
  personas: {
    id: string;
    nombre: string;
    apellido: string;
    documento: string;
    legajo: string | null;
    designacionesVigentes: {
      materiaId: string;
      materiaNombre: string;
      cargoId: string;
      cargoNombre: string;
      dedicacion: string | null;
      horas: number;
      horasInvestigacion: number | null;
      horasExternas: number | null;
    }[];
  }[];
  cargos: { id: string; codigo: string; nombre: string; abreviatura: string; orden: number }[];
  dedicaciones: { id: string; codigo: number; nombre: string; orden: number }[];
  tiposBaja: string[];
  novedades: string[];
}

export async function obtenerCatalogosDesignaciones(): Promise<CatalogosDesignaciones> {
  return (await apiClient.get<CatalogosDesignaciones>("/api/designaciones/catalogos")).data;
}

export function docentesDesdeCatalogo(catalogos: CatalogosDesignaciones): DocenteExistente[] {
  return catalogos.personas.flatMap((persona) => {
    const primera = persona.designacionesVigentes[0];
    if (!primera) return [];
    return [
      {
        personaId: persona.id,
        dni: persona.documento,
        nombre: `${persona.apellido}, ${persona.nombre}`,
        legajo: persona.legajo ?? "",
        antiguedad: 0,
        cargoActual: primera.cargoNombre,
        dedicacionActual: (primera.dedicacion ?? "") as Dedicacion,
        materiasActuales: persona.designacionesVigentes.map((d) => ({
          materiaId: d.materiaId,
          materia: d.materiaNombre,
          horas: d.horas,
          cargoActual: d.cargoNombre,
          dedicacionActual: d.dedicacion,
          horasInvestigacion: d.horasInvestigacion,
          horasExternas: d.horasExternas,
        })),
        horasInvestigacionActuales: primera.horasInvestigacion,
        horasExternasActuales: primera.horasExternas,
      },
    ];
  });
}

export function formatearDni(dni: string): string {
  const limpio = dni.replace(/\D/g, "");
  return limpio ? limpio.replace(/\B(?=(\d{3})+(?!\d))/g, ".") : dni;
}
export function asignacionVigenteEnMateria(
  docente: DocenteExistente | undefined,
  materiaId: string | undefined,
) {
  return materiaId
    ? docente?.materiasActuales.find((asignacion) => asignacion.materiaId === materiaId)
    : undefined;
}
