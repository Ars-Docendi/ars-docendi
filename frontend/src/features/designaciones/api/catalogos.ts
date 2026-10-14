import { apiClient } from "../../../shared/api/client";
import type { Dedicacion, DocenteExistente, PeriodoDesignacion } from "../types";

interface MateriaCatalogoDto {
  materiaId: string;
  codigo: string;
  nombre: string;
  carreraId: string;
  carreraNombre: string;
}

interface CatalogosDesignacionesDto extends Omit<CatalogosDesignaciones, "materias"> {
  materias: MateriaCatalogoDto[];
}

export interface CatalogosDesignaciones {
  periodoActivo: PeriodoDesignacion | null;
  periodos: PeriodoDesignacion[];
  /**
   * Pares materia–carrera: `id` es la materia canónica. Una materia dictada en más de una
   * carrera aparece una vez por carrera (el pedido manda ambos ids juntos).
   */
  materias: {
    id: string;
    codigo: string;
    nombre: string;
    carreraId: string;
    carreraNombre: string;
  }[];
  personas: {
    id: string;
    nombre: string;
    apellido: string;
    documento: string;
    legajo: string | null;
    designacionesVigentes: {
      materiaId: string;
      materiaNombre: string;
      carreraId: string;
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
  const dto = (await apiClient.get<CatalogosDesignacionesDto>("/api/designaciones/catalogos")).data;
  return {
    ...dto,
    materias: dto.materias.map((m) => ({
      id: m.materiaId,
      codigo: m.codigo,
      nombre: m.nombre,
      carreraId: m.carreraId,
      carreraNombre: m.carreraNombre,
    })),
  };
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
          carreraId: d.carreraId,
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
