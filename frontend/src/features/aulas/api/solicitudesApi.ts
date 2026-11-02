import { apiClient } from "../../../shared/api/client";
import type {
  DatosNuevaSolicitud,
  DocenteSolicitud,
  MateriaOpcion,
  SolicitudReservaAula,
} from "../types";

interface SolicitudReservaAulaDto {
  id: string;
  dia: string;
  horarioDesde: string;
  horarioHasta: string;
  cantidadAlumnosAprox: number;
  materia: MateriaOpcion;
  comision: string;
  estado: string;
  aulaAsignada: string | null;
  aulaId: string | null;
  motivoRechazo: string | null;
  motivoCancelacion: string | null;
  creadoEn: string;
  docente: DocenteSolicitud | null;
  esPropia: boolean;
}

export async function listarMateriasPropias(): Promise<MateriaOpcion[]> {
  return (await apiClient.get<MateriaOpcion[]>("/api/aulas/solicitudes/materias-propias")).data;
}

export async function listarMisSolicitudes(): Promise<SolicitudReservaAula[]> {
  return (await apiClient.get<SolicitudReservaAulaDto[]>("/api/aulas/solicitudes/mias")).data.map(
    mapear,
  );
}

export async function listarTodasLasSolicitudes(): Promise<SolicitudReservaAula[]> {
  return (await apiClient.get<SolicitudReservaAulaDto[]>("/api/aulas/solicitudes")).data.map(
    mapear,
  );
}

/** Detalle de una solicitud: lo ve su dueño o quien resuelve; para el resto es 404. */
export async function obtenerSolicitud(id: string): Promise<SolicitudReservaAula> {
  return mapear(
    (await apiClient.get<SolicitudReservaAulaDto>(`/api/aulas/solicitudes/${id}`)).data,
  );
}

export async function crearSolicitud(datos: DatosNuevaSolicitud): Promise<SolicitudReservaAula> {
  return mapear(
    (await apiClient.post<SolicitudReservaAulaDto>("/api/aulas/solicitudes", datos)).data,
  );
}

export async function cancelarSolicitud(id: string): Promise<void> {
  await apiClient.post(`/api/aulas/solicitudes/${id}/cancelar`);
}

export async function asignarAula(id: string, aulaId: string): Promise<SolicitudReservaAula> {
  return mapear(
    (
      await apiClient.post<SolicitudReservaAulaDto>(`/api/aulas/solicitudes/${id}/asignar`, {
        aulaId,
      })
    ).data,
  );
}

export async function rechazarSolicitud(id: string, motivo: string): Promise<SolicitudReservaAula> {
  return mapear(
    (
      await apiClient.post<SolicitudReservaAulaDto>(`/api/aulas/solicitudes/${id}/rechazar`, {
        motivo,
      })
    ).data,
  );
}

export async function cancelarReserva(id: string, motivo: string): Promise<SolicitudReservaAula> {
  return mapear(
    (
      await apiClient.post<SolicitudReservaAulaDto>(
        `/api/aulas/solicitudes/${id}/cancelar-reserva`,
        {
          motivo,
        },
      )
    ).data,
  );
}

function mapear(dto: SolicitudReservaAulaDto): SolicitudReservaAula {
  return {
    id: dto.id,
    dia: dto.dia,
    horarioDesde: dto.horarioDesde,
    horarioHasta: dto.horarioHasta,
    cantidadAlumnosAprox: dto.cantidadAlumnosAprox,
    materia: dto.materia,
    comision: dto.comision,
    estado: dto.estado as SolicitudReservaAula["estado"],
    aulaAsignada: dto.aulaAsignada ?? undefined,
    aulaId: dto.aulaId ?? undefined,
    motivoRechazo: dto.motivoRechazo ?? undefined,
    motivoCancelacion: dto.motivoCancelacion ?? undefined,
    creadoEn: dto.creadoEn,
    docente: dto.docente ?? undefined,
    esPropia: dto.esPropia,
  };
}
