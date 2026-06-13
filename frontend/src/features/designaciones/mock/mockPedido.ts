// Store mock local del Pedido de Designación — mismo enfoque que features/usuarios:
// estado en memoria, sin HTTP real. La validación vive en helpers puros para
// reutilizarse el día que entre el backend.

export type TipoPedido = "alta-nueva" | "renovacion" | "cambio" | "baja";

export type EstadoPantalla = "edit" | "loading" | "error";

/** Los tres documentos que "Alta nueva" exige sí o sí. */
export type DocumentoRequeridoId = "cv" | "dni-frente" | "dni-dorso";

export interface ArchivoCargado {
  id: string;
  name: string;
  /** Tamaño legible, ej. "1.2 MB". */
  size?: string;
}

export interface DocumentacionPedido {
  cv: ArchivoCargado | null;
  dniFrente: ArchivoCargado | null;
  dniDorso: ArchivoCargado | null;
  otros: ArchivoCargado[];
}

export interface DesignacionSolicitada {
  materia: string;
  comision: string;
  cargo: string;
  horas: string;
  dedicacion: string;
  antiguedad: string;
}

export interface DatosDocente {
  documento: string;
  nombreApellido: string;
  legajo: string;
  emailInstitucional: string;
  telefono: string;
}

export interface PedidoMock {
  id: string;
  numero: string;
  tipo: TipoPedido;
  docente: DatosDocente;
  designacion: DesignacionSolicitada;
  justificacion: string;
  documentacion: DocumentacionPedido;
}

export const JUSTIFICACION_MIN = 20;
export const JUSTIFICACION_MAX = 1000;

/** Tamaño máximo permitido por archivo en la sección Documentación (5 MB). */
export const TAMANO_MAX_BYTES = 5 * 1024 * 1024;

export const TIPOS_PEDIDO: {
  id: TipoPedido;
  nombre: string;
  descripcion: string;
}[] = [
  {
    id: "alta-nueva",
    nombre: "Alta nueva",
    descripcion: "Docente que se incorpora a la cátedra por primera vez.",
  },
  {
    id: "renovacion",
    nombre: "Renovación",
    descripcion: "Continuidad de un docente actual para el próximo cuatrimestre.",
  },
  {
    id: "cambio",
    nombre: "Cambio de cargo",
    descripcion: "Promoción o modificación de horas / dedicación.",
  },
  {
    id: "baja",
    nombre: "Baja",
    descripcion: "Cierre de la designación al fin de cuatrimestre.",
  },
];

export const MATERIAS = ["Análisis Matemático I", "Análisis Matemático II", "Álgebra II"];
export const CARGOS = ["Auxiliar de 1ª", "Jefe de Trabajos Prácticos", "Adjunto"];
export const DEDICACIONES = ["Simple", "Semi-exclusiva", "Exclusiva"];

/** Pedido semilla para el estado default del diseño (renovación, documentación cargada). */
export function pedidoInicial(): PedidoMock {
  return {
    id: "2026-0418",
    numero: "#2026-0418",
    tipo: "renovacion",
    docente: {
      documento: "30.245.918",
      nombreApellido: "María Álvarez",
      legajo: "L-04812",
      emailInstitucional: "malvarez@unlam.edu.ar",
      telefono: "+54 11 4567-1234",
    },
    designacion: {
      materia: "Análisis Matemático I",
      comision: "02 · Cát. Ruiz · Noche",
      cargo: "Auxiliar de 1ª",
      horas: "10",
      dedicacion: "Simple",
      antiguedad: "8 años",
    },
    justificacion:
      "Reemplazo por jubilación del Auxiliar saliente. La docente cuenta con currículum acorde " +
      "(Lic. en Matemática · UBA · 2018) y experiencia previa en cátedras similares de la UNLP. " +
      "La cátedra necesita cubrir la comisión 02 de la noche, que el saliente venía sosteniendo desde 2019.",
    documentacion: {
      cv: { id: "cv", name: "CV-Alvarez-Maria.pdf", size: "1.2 MB" },
      dniFrente: { id: "dni-frente", name: "DNI-frente.jpg", size: "842 KB" },
      dniDorso: { id: "dni-dorso", name: "DNI-dorso.jpg", size: "810 KB" },
      otros: [],
    },
  };
}

/** Pedido semilla para el estado "alta nueva" del diseño (documentación obligatoria vacía). */
export function pedidoAltaNueva(): PedidoMock {
  const base = pedidoInicial();
  return {
    ...base,
    tipo: "alta-nueva",
    docente: { ...base.docente, legajo: "", emailInstitucional: "" },
    designacion: { ...base.designacion, antiguedad: "0 años" },
    documentacion: { cv: null, dniFrente: null, dniDorso: null, otros: [] },
  };
}

/** True cuando el archivo supera el tamaño máximo permitido por documento. */
export function excedeTamanoMaximo(file: File): boolean {
  return file.size > TAMANO_MAX_BYTES;
}

/** True cuando el tipo exige CV + DNI (frente y dorso). */
export function exigeDocumentacion(tipo: TipoPedido): boolean {
  return tipo === "alta-nueva";
}

/** Lista de documentos obligatorios todavía sin cargar (vacía si el tipo no exige). */
export function documentosFaltantes(pedido: PedidoMock): DocumentoRequeridoId[] {
  if (!exigeDocumentacion(pedido.tipo)) return [];
  const faltan: DocumentoRequeridoId[] = [];
  if (!pedido.documentacion.cv) faltan.push("cv");
  if (!pedido.documentacion.dniFrente) faltan.push("dni-frente");
  if (!pedido.documentacion.dniDorso) faltan.push("dni-dorso");
  return faltan;
}

/** True cuando el pedido cumple las reglas mínimas para enviarse a revisión. */
export function puedeEnviar(pedido: PedidoMock): boolean {
  if (pedido.justificacion.trim().length < JUSTIFICACION_MIN) return false;
  return documentosFaltantes(pedido).length === 0;
}
