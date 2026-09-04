// ============================================================
// Tipos del Portal del docente (autogestión del perfil).
// El perfil es el CV del docente convertido en datos.
// ============================================================

/**
 * Datos que el docente NO posee: identidad de Azure AD (nombre, apellido, UPN)
 * y datos institucionales de Secretaría (documento, legajo, CUIL). Se muestran
 * de solo lectura. El teléfono NO va acá: es del docente (ver `DatosContacto`).
 */
export interface PerfilInstitucional {
  nombre: string;
  apellido: string;
  /** Mail institucional / UPN de Azure AD. */
  upn: string;
  documento: string;
  legajo: string;
  cuil: string;
}

/** Contacto que el docente mantiene. Ambos campos son opcionales. */
export interface DatosContacto {
  telefono: string;
  /** Mail de contacto, distinto del institucional. */
  mail: string;
}

/**
 * CV del docente: un único archivo, sin historial. Metadata mock — la
 * persistencia real del archivo es backend.
 */
export interface ArchivoCv {
  nombre: string;
  /** Fecha de carga en ISO (YYYY-MM-DD). */
  fechaCarga: string;
}

/** Un período con fin opcional. `hasta: null` significa vigente ("actual"). */
export interface Periodo {
  desde: string;
  hasta: string | null;
}

export interface Experiencia extends Periodo {
  id: string;
  puesto: string;
  organizacion: string;
  descripcion: string;
}

export const NIVELES_EDUCACION = ["Grado", "Especialización", "Maestría", "Doctorado"] as const;

export type NivelEducacion = (typeof NIVELES_EDUCACION)[number];

export interface Educacion extends Periodo {
  id: string;
  nivel: NivelEducacion;
  /** Carrera o título obtenido. */
  carrera: string;
  institucion: string;
}

export interface Certificacion {
  id: string;
  nombre: string;
  emisor: string;
  fecha: string;
  /** Muchas certificaciones caducan; las que no, dejan esto en null. */
  vencimiento: string | null;
}

/** Documento adjunto a un proyecto. Metadata mock, sin storage real. */
export interface DocumentoAdjunto {
  nombre: string;
}

/**
 * Un proyecto del docente. Los trabajos de investigación y su documentación
 * viven acá: no hay una sección aparte de producción científica.
 */
export interface Proyecto extends Periodo {
  id: string;
  nombre: string;
  rol: string;
  descripcion: string;
  /** Documento en PDF, si lo hay. */
  documento: DocumentoAdjunto | null;
  /** Enlace DOI, si lo hay. Vacío significa sin enlace. */
  doi: string;
}

/**
 * Término de habilidad o interés. `sugerido` marca los que el docente propuso
 * y todavía no están incorporados al vocabulario.
 */
export interface Tag {
  termino: string;
  sugerido: boolean;
}

/** El perfil completo del docente autenticado. */
export interface PerfilDocente {
  institucional: PerfilInstitucional;
  contacto: DatosContacto;
  cv: ArchivoCv | null;
  experiencia: Experiencia[];
  educacion: Educacion[];
  certificaciones: Certificacion[];
  proyectos: Proyecto[];
  habilidades: Tag[];
  intereses: Tag[];
}

/** Estado de la lectura del perfil en la pantalla. */
export type EstadoPerfil = "cargando" | "error" | "listo";

/** Datos editables de cada tipo de ítem (lo que producen los formularios). */
export type DatosExperiencia = Omit<Experiencia, "id">;
export type DatosEducacion = Omit<Educacion, "id">;
export type DatosCertificacion = Omit<Certificacion, "id">;
export type DatosProyecto = Omit<Proyecto, "id">;
