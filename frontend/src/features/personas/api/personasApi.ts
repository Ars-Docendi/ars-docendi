import { apiClient } from "../../../shared/api/client";
import { crearApiPerfil } from "../../../shared/perfil/api/portalApi";
import type { MembresiaGuardar } from "../../../shared/ui/membresias";

export interface RolResumen {
  id: string;
  codigo: string;
  nombre: string;
}

export interface Membresia {
  id: string;
  rolId: string;
  codigo: string;
  nombre: string;
  ambito: string;
  materiaId: string | null;
  carreraId: string | null;
}

export interface Asignacion {
  id: string;
  materiaId: string;
  materiaCodigo: string;
  materiaNombre: string;
  carreraId: string;
  cargoId: string;
  cargoNombre: string;
  cargoAbreviatura: string;
  dedicacion: string | null;
  dedicacionId: string | null;
  horas: number;
}

export interface Persona {
  personaId: string;
  usuarioId: string | null;
  nombre: string;
  apellido: string;
  documento: string;
  legajo: string | null;
  cuil: string | null;
  fechaNacimiento: string | null;
  telefono: string | null;
  upn: string | null;
  tieneCuenta: boolean;
  activo: boolean;
  version: number | null;
  roles: RolResumen[];
  membresias: Membresia[];
  asignaciones: Asignacion[];
}

export interface OpcionCatalogo {
  id: string;
  codigo: string;
  nombre: string;
  carreraId?: string | null;
  carreraNombre?: string | null;
  carreraCodigo?: string | null;
}

export interface CatalogosPersonas {
  roles: { id: string; codigo: string; nombre: string; ambito: string; esSistema: boolean }[];
  materias: OpcionCatalogo[];
  materiasPlan: OpcionCatalogo[];
  carreras: OpcionCatalogo[];
  cargos: { id: string; codigo: string; nombre: string; abreviatura: string; activo: boolean }[];
  dedicaciones: { id: string; nombre: string; activo: boolean }[];
}

export interface GuardarPersona {
  nombre: string;
  apellido: string;
  documento: string;
  legajo: string | null;
  cuil: string | null;
  fechaNacimiento: string | null;
  telefono: string | null;
  upn: string;
  membresias: MembresiaGuardar[];
  designaciones: {
    materiaId: string;
    carreraId: string;
    cargoId: string;
    dedicacionId: string | null;
    horas: number;
  }[];
  version?: number | null;
}

export interface DocumentoRegistrado {
  existe: boolean;
  personaId?: string;
  nombre?: string;
  legajo?: string | null;
  /** Estado de la cuenta; `null` si la persona no tiene cuenta. */
  activo?: boolean | null;
}

const base = "/api/administracion/personas";

export const personasKeys = {
  todas: ["personas"] as const,
  detalle: (id: string) => ["personas", id] as const,
  catalogos: ["personas", "catalogos"] as const,
  portal: (id: string) => ["personas", id, "portal"] as const,
};

export async function listarPersonas() {
  return (await apiClient.get<Persona[]>(base)).data;
}

export async function obtenerPersona(id: string) {
  return (await apiClient.get<Persona>(`${base}/${id}`)).data;
}

export async function obtenerCatalogosPersonas() {
  return (await apiClient.get<CatalogosPersonas>(`${base}/catalogos`)).data;
}

export async function crearPersona(datos: GuardarPersona) {
  return (await apiClient.post<Persona>(base, datos)).data;
}

export async function editarPersona(id: string, datos: GuardarPersona) {
  return (await apiClient.put<Persona>(`${base}/${id}`, datos)).data;
}

export async function cambiarEstadoPersona(persona: Persona, activar: boolean) {
  return (
    await apiClient.post<Persona>(
      `${base}/${persona.personaId}/${activar ? "activar" : "desactivar"}`,
      {
        version: persona.version,
      },
    )
  ).data;
}

export async function verificarDocumento(documento: string, exceptoPersonaId?: string) {
  return (
    await apiClient.post<DocumentoRegistrado>(`${base}/documento/verificar`, {
      documento,
      exceptoPersonaId,
    })
  ).data;
}

/** Mi Portal de la persona, con la misma API que el perfil propio. */
export const apiPortalDePersona = (personaId: string) =>
  crearApiPerfil(`${base}/${personaId}/portal`);
