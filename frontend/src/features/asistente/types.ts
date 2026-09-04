// ============================================================
// El contrato de POST /api/asistente/consultas y GET /api/asistente/capacidades.
// Ver docs/architecture/api-contracts.md §Asistente.
// ============================================================

/**
 * Los cuatro estados en que puede terminar un turno.
 *
 * `no_contestable` y `necesita_aclaracion` NO son lo mismo, y colapsarlos en la
 * interfaz haría que el asistente diga «no puedo» cuando corresponde «¿cuál de
 * estas?». `servicio_degradado` tampoco es un error del usuario: su pregunta no
 * tiene nada de malo.
 */
export type EstadoDelTurno =
  | "respondida"
  | "no_contestable"
  | "necesita_aclaracion"
  | "servicio_degradado";

export interface OpcionDeAclaracion {
  etiqueta: string;
  preguntaResuelta: string;
}

export interface ColumnaDelResultado {
  nombre: string;
  /** Si trae un dato personal: no viajó al modelo, viene directo del motor. */
  sensible: boolean;
}

export interface MetricasDelTurno {
  llamadasAlModelo: number;
  // El backend también manda `categoria` («consulta_simple», «cruce_de_tablas»…) y
  // acá no se declara A PROPÓSITO: es la etiqueta interna del carril que resolvió
  // el turno, y RNF-18 prohíbe mostrar etiquetas internas. Lo que no está en el
  // tipo no se puede pintar por descuido.
}

export interface RespuestaDelAsistente {
  estado: EstadoDelTurno;
  respuesta: string;
  hilo: string;
  preguntaInterpretada?: string | null;
  razonamiento?: string | null;
  /** Bloquean el turno: hay que elegir una para seguir. */
  opciones: OpcionDeAclaracion[];
  /** NO bloquean nada: son preguntas nuevas que se sabe que funcionan. */
  sugerencias: string[];
  columnas: ColumnaDelResultado[];
  filas: unknown[][];
  /** Booleano y nunca un conteo: cuántas filas faltan es un canal de inferencia. */
  truncado: boolean;
  /** Solo llega con el permiso `asistente.ver_consulta`. */
  sql?: string | null;
  metricas: MetricasDelTurno;
}

export interface AreaCubierta {
  nombre: string;
  descripcion?: string | null;
  columnas: number;
}

export interface CapacidadesDelAsistente {
  cubre: AreaCubierta[];
  tablas: number;
  columnas: number;
  ejemplos: string[];
  noPuede: string[];
  /** Qué filas ve. Va aparte de los conteos: el ámbito no cambia qué se puede preguntar. */
  alcance: string;
}

/** Un turno ya renderizable, del lado del cliente. */
export interface TurnoDeLaConversacion {
  id: string;
  pregunta: string;
  /** Ausente mientras el turno está en vuelo. */
  respuesta?: RespuestaDelAsistente;
  /** Mensaje comprensible cuando el pedido falló por transporte. */
  error?: string;
  /** El usuario dejó de esperarlo: el request se soltó de este lado. No es un error. */
  detenido?: boolean;
}
