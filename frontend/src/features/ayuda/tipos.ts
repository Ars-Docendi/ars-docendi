/** Modelo del contenido del Centro de ayuda (objetos tipados en el repositorio). */

export type TipoNotaAyuda = "info" | "importante" | "consejo";

export interface CapturaAyuda {
  /** Ruta absoluta bajo `frontend/public`, p. ej. `/ayuda/tareas/crear-una-tarea-paso-2.webp`. */
  src: string;
  alt: string;
  leyenda?: string;
}

export interface NotaAyuda {
  tipo: TipoNotaAyuda;
  titulo?: string;
  cuerpo: string;
}

export interface PasoAyuda {
  titulo: string;
  /** Etiqueta corta para la lista de pasos de la sidebar; por defecto, el título. */
  etiquetaIndice?: string;
  cuerpo: string;
  captura?: CapturaAyuda;
  nota?: NotaAyuda;
}

export interface ViñetaAyuda {
  termino: string;
  descripcion: string;
}

export interface DespuesAyuda {
  titulo: string;
  cuerpo: string;
  viñetas?: readonly ViñetaAyuda[];
}

export interface PreguntaAyuda {
  pregunta: string;
  respuesta: string;
}

export interface ModuloAyuda {
  slug: string;
  titulo: string;
  descripcion: string;
  /** Orden de presentación de los grupos de artículos. */
  grupos: readonly string[];
  /** Cualquiera de estos permisos alcanza; vacío = todo usuario autenticado. */
  permisos: readonly string[];
}

export interface ArticuloAyuda {
  slug: string;
  moduloSlug: string;
  grupo: string;
  titulo: string;
  resumen: string;
  introduccion: string;
  /** Cualquiera de estos permisos alcanza; vacío = todo usuario autenticado. */
  permisos: readonly string[];
  palabrasClave?: readonly string[];
  pasos: readonly PasoAyuda[];
  despues?: DespuesAyuda;
  preguntas?: readonly PreguntaAyuda[];
  /** Referencias `modulo/articulo`. */
  relacionados: readonly string[];
}

/** Conjunto de contenido sobre el que operan visibilidad y búsqueda. */
export interface ContenidoAyuda {
  modulos: readonly ModuloAyuda[];
  articulos: readonly ArticuloAyuda[];
}
