import { apiClient } from "../../../shared/api/client";
import type { CatalogosDesignaciones } from "./catalogos";
import type {
  AccionHistorial,
  Cargo,
  DatosEditablesPedido,
  EstadoPedido,
  Novedad,
  PedidoDesignacion,
  Rol,
  TipoAdjunto,
} from "../types";

interface PedidoDto {
  id: string;
  numero: string;
  periodo: { id: string; nombre: string };
  persona: {
    id: string;
    nombre: string;
    apellido: string;
    documento: string;
    legajo: string | null;
  };
  materia: { id: string; codigo: string; nombre: string; carreraId: string; carreraNombre: string };
  novedad: Novedad;
  estado: EstadoPedido;
  prioritario: boolean;
  cargoSolicitado: { id: string; codigo: string; nombre: string } | null;
  dedicacionSolicitada: string | null;
  horas: number | null;
  horasInvestigacion: number | null;
  horasExternas: number | null;
  justificacion: string | null;
  tipoBaja: string | null;
  tipoBajaDetalle: string | null;
  etapaRetorno: EstadoPedido | null;
  propietarioActual: string | null;
  snapshot: { cargo: string | null; dedicacion: string | null } | null;
  version: number;
  adjuntos: { id: string; tipo: TipoAdjunto; nombre: string }[];
  historial: {
    id: string;
    accion: AccionHistorial;
    rolCodigo: string;
    actorNombre: string | null;
    etapa: EstadoPedido;
    comentario: string | null;
    creadoEn: string;
  }[];
  accionesPermitidas: string[];
}

export async function listarMisPedidos(): Promise<PedidoDesignacion[]> {
  return listarPedidosPorAmbito();
}
export async function listarPedidosPorAmbito(): Promise<PedidoDesignacion[]> {
  return (await apiClient.get<PedidoDto[]>("/api/designaciones/pedidos")).data.map(mapear);
}
export async function obtenerPedido(id: string): Promise<PedidoDesignacion> {
  return mapear((await apiClient.get<PedidoDto>(`/api/designaciones/pedidos/${id}`)).data);
}
export async function crearPedido(datos: DatosEditablesPedido, catalogos: CatalogosDesignaciones) {
  return mapear(
    (await apiClient.post<PedidoDto>("/api/designaciones/pedidos", payload(datos, catalogos))).data,
  );
}
export async function editarPedido(
  id: string,
  datos: DatosEditablesPedido,
  catalogos: CatalogosDesignaciones,
) {
  return mapear(
    (await apiClient.put<PedidoDto>(`/api/designaciones/pedidos/${id}`, payload(datos, catalogos)))
      .data,
  );
}
export async function eliminarPedido(id: string): Promise<void> {
  await apiClient.delete(`/api/designaciones/pedidos/${id}`);
}
export const enviarPedido = (id: string) => accion(id, "enviar");
export const reenviarPedido = (id: string) => accion(id, "reenviar");
export const aceptarPedido = (id: string, comentario?: string) => accion(id, "aceptar", comentario);
export const rechazarPedido = (id: string, comentario?: string) =>
  accion(id, "rechazar", comentario);
export const devolverPedido = (id: string, comentario?: string) =>
  accion(id, "devolver", comentario);
export const priorizarPedido = (id: string, comentario?: string) =>
  accion(id, "priorizar", comentario);
export const despriorizarPedido = (id: string, comentario?: string) =>
  accion(id, "despriorizar", comentario);

async function accion(id: string, nombre: string, comentario?: string): Promise<PedidoDesignacion> {
  const { data } = await apiClient.post<PedidoDto>(
    `/api/designaciones/pedidos/${id}/${nombre}`,
    { comentario: comentario ?? null },
    { headers: { "Idempotency-Key": crypto.randomUUID() } },
  );
  return mapear(data);
}

function payload(datos: DatosEditablesPedido, catalogos: CatalogosDesignaciones) {
  const personaId =
    datos.personaId ??
    catalogos.personas.find((p) => p.documento === datos.docente.dni.replace(/\D/g, ""))?.id;
  const materiaId =
    datos.materiaId ?? catalogos.materias.find((m) => m.nombre === datos.catedra)?.id;
  const cargoSolicitadoId =
    datos.cargoSolicitadoId ??
    catalogos.cargos.find(
      (c) => c.nombre === datos.cargoSolicitado || c.abreviatura === datos.cargoSolicitado,
    )?.id;
  return {
    periodoId: datos.periodoId ?? catalogos.periodoActivo?.id,
    personaId,
    materiaId,
    novedad: datos.novedad,
    cargoSolicitadoId,
    dedicacionSolicitada: datos.dedicacionSolicitada ?? null,
    horas: datos.horas,
    horasInvestigacion: datos.horasInvestigacion,
    horasExternas: datos.horasExternas,
    justificacion: datos.justificacion ?? null,
    tipoBaja: datos.tipoBaja ?? null,
    tipoBajaDetalle: datos.tipoBajaDetalle ?? null,
    adjuntos: datos.adjuntos.map((a) => ({ tipo: a.tipo, nombre: a.nombre })),
    version: datos.version,
  };
}

function mapear(dto: PedidoDto): PedidoDesignacion {
  return {
    id: dto.id,
    numero: dto.numero,
    periodoId: dto.periodo.id,
    periodoNombre: dto.periodo.nombre,
    personaId: dto.persona.id,
    materiaId: dto.materia.id,
    catedra: dto.materia.nombre,
    carrera: dto.materia.carreraNombre,
    docente: {
      dni: dto.persona.documento,
      nombre: `${dto.persona.apellido}, ${dto.persona.nombre}`,
      legajo: dto.persona.legajo ?? undefined,
      antiguedad: 0,
    },
    horas: dto.horas ?? 0,
    cargoActual: (dto.snapshot?.cargo as Cargo) ?? null,
    dedicacionActual: dto.snapshot?.dedicacion ?? null,
    novedad: dto.novedad,
    cargoSolicitado: dto.cargoSolicitado?.nombre,
    cargoSolicitadoId: dto.cargoSolicitado?.id,
    dedicacionSolicitada: dto.dedicacionSolicitada ?? undefined,
    justificacion: dto.justificacion ?? undefined,
    tipoBaja: dto.tipoBaja as PedidoDesignacion["tipoBaja"],
    tipoBajaDetalle: dto.tipoBajaDetalle ?? undefined,
    horasExternas: dto.horasExternas ?? 0,
    horasInvestigacion: dto.horasInvestigacion ?? 0,
    adjuntos: dto.adjuntos,
    estado: dto.estado,
    prioritario: dto.prioritario,
    etapaRetorno: dto.etapaRetorno ?? undefined,
    propietarioActual: rolVisible(dto.propietarioActual) ?? undefined,
    historial: dto.historial.map((h) => ({
      id: h.id,
      accion: h.accion,
      porRol: rolVisible(h.rolCodigo) ?? "Administración",
      porNombre: h.actorNombre ?? "Sistema",
      etapa: h.etapa,
      comentario: h.comentario ?? undefined,
      fecha: h.creadoEn,
    })),
    accionesPermitidas: dto.accionesPermitidas,
    version: dto.version,
  };
}

function rolVisible(codigo: string | null): Rol | null {
  return (
    (
      {
        jefe_catedra: "Jefe de Cátedra",
        coordinador_carrera: "Coordinador",
        secretaria: "Secretaría",
        decanato: "Decanato",
        administrativo: "Administración",
        docente: "Docente",
      } as Record<string, Rol | undefined>
    )[codigo ?? ""] ?? null
  );
}
