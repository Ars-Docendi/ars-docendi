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
  "respondida" | "no_contestable" | "necesita_aclaracion" | "servicio_degradado";

export interface OpcionDeAclaracion {
  etiqueta: string;
  preguntaResuelta: string;
}

export interface ColumnaDelResultado {
  nombre: string;
  /** Si trae un dato personal: no viajó al modelo, viene directo del motor. */
  sensible: boolean;
}

/**
 * Una celda que identifica algo que el usuario puede abrir.
 *
 * NO TRAE LA URL, y eso es del contrato: el backend dice QUÉ —qué clase de cosa y
 * con qué identificador— y la ruta la resuelve el cliente, que es donde viven las
 * rutas. Un `tipo` que este cliente no conoce no se pinta, así que un backend que
 * empiece a mandar uno nuevo no rompe nada.
 *
 * Que una fila esté en `filas` no implica que traiga vínculo: las filas las filtra
 * el motor y la pantalla la autoriza el módulo dueño, que son dos reglas distintas.
 */
export interface VinculoDelResultado {
  fila: number;
  columna: number;
  tipo: string;
  id: string;
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
  /**
   * Bloquean el turno: hay que elegir una para seguir. Sólo llega con
   * `estado = "necesita_aclaracion"`. Ningún otro estado ofrece preguntas
   * nuevas para probar — desde ARS-149 el único lugar con ejemplos clicables
   * es la bienvenida (`capacidades.ejemplos`).
   */
  opciones: OpcionDeAclaracion[];
  columnas: ColumnaDelResultado[];
  filas: unknown[][];
  /** Booleano y nunca un conteo: cuántas filas faltan es un canal de inferencia. */
  truncado: boolean;
  /** Las celdas que llevan a una pantalla del sistema. Vacío si ninguna. */
  vinculos: VinculoDelResultado[];
  /** Solo llega con el permiso `asistente.ver_consulta`. */
  sql?: string | null;
  metricas: MetricasDelTurno;
  /**
   * Present only when `estado` is `"respondida"`. Submit it once to
   * `POST /api/asistente/retroalimentacion` to rate this turn. Never identifies
   * who asked — see asistente-retroalimentacion's spec.
   */
  claveDeRetroalimentacion?: string | null;
  /**
   * El cupo diario del actor INMEDIATAMENTE DESPUÉS de que este turno se
   * cobrara (asistente-cupo-visible) — nunca el valor de antes. La franja de
   * estado lo usa para actualizarse sin volver a pedir `capacidades`.
   */
  cupoRestante?: number | null;
  /**
   * El id de `GET /historial` en el que quedó este turno (design.md D13 de
   * asistente-rediseno-v3). Ausente si no se persistió: el rail no resalta ni
   * titula nada con este turno, y el que ya tenía la conversación activa —si
   * lo había— no se pisa.
   */
  conversacion?: string | null;
}

export interface AreaCubierta {
  nombre: string;
  descripcion?: string | null;
  columnas: number;
}

// ============================================================
// Mantenimiento y cupo (asistente-modo-mantenimiento / asistente-cupo-visible).
// Ver docs/architecture/api-contracts.md §GET /api/asistente/capacidades.
// ============================================================

/** El modo mantenimiento, global y sin bypass: consistente para todo el mundo. */
export interface MantenimientoDelAsistente {
  activo: boolean;
  razon?: string | null;
}

/**
 * Uno de los tres motivos por los que un turno puede estar bloqueado. El texto
 * lo elige la interfaz; el backend sólo manda cuál de los tres es.
 */
export type MotivoDeBloqueo = "presupuesto_propio" | "tope_organizacional" | "mantenimiento";

/**
 * El cupo diario de ESTE actor, con el bypass de mantenimiento del admin ya
 * aplicado del lado del backend (a diferencia de `mantenimiento`, que es
 * global y sin bypass).
 */
export interface CupoDelActor {
  /** `2147483647` (`int.MaxValue`) cuando el cupo está desactivado. */
  restante: number;
  bloqueado: boolean;
  motivo?: MotivoDeBloqueo | null;
  /** Sólo se conoce para `presupuesto_propio`. */
  vuelveA?: string | null;
}

/** El valor de `restante` cuando el cupo diario está desactivado (0 = sin tope). */
export const CUPO_SIN_LIMITE = 2147483647;

export interface CapacidadesDelAsistente {
  cubre: AreaCubierta[];
  tablas: number;
  columnas: number;
  ejemplos: string[];
  noPuede: string[];
  /** Qué filas ve. Va aparte de los conteos: el ámbito no cambia qué se puede preguntar. */
  alcance: string;
  /**
   * Por qué cosas suele venir a preguntar este usuario, según su rol. La escribe el
   * backend —el cliente no tiene catálogo de roles ni debería crecer uno— y un rol
   * que no reconoce recibe un texto genérico.
   */
  presentacion: string;
  mantenimiento: MantenimientoDelAsistente;
  cupo: CupoDelActor;
}

// ============================================================
// Panel de uso administrativo (asistente-panel-de-uso,
// asistente-presupuesto-persistente). Sólo lo ve quien tiene
// `asistente.administrar`. Ver docs/architecture/api-contracts.md
// §GET /api/asistente/administracion/uso.
// ============================================================

/** Un agregado de uso: por usuario, por rol, u organizacional. */
export interface UsoAgregado {
  clave: string;
  /** Sólo en los agregados por usuario (resuelto vía `IConsultasIdentity`). */
  nombreParaMostrar?: string | null;
  turnos: number;
  porEstado: Record<string, number>;
  llamadasAlModelo: number;
  tokensDeEntrada: number;
  tokensDeSalida: number;
  tokensDeCache: number;
  latenciaPromedioMs: number;
  latenciaP95Ms: number;
  proveedores: string[];
  /** Siempre una ESTIMACIÓN: la factura del proveedor es la fuente de verdad. */
  costoEstimado: number;
  /** Siempre `true`: nunca se muestra sin la etiqueta de estimado. */
  esEstimado: boolean;
  /** Turnos cuyo proveedor/modelo no tiene precio vigente: NUNCA costeados en 0. */
  turnosSinPrecio: number;
}

/** El panel de uso completo (`GET /api/asistente/administracion/uso`). */
export interface UsoDelAsistente {
  porUsuario: UsoAgregado[];
  porRol: UsoAgregado[];
  organizacion: UsoAgregado;
}

/** Período del panel de uso: relativo (`día`/`semana`/`mes`) o un rango explícito. */
export type PeriodoDeUso = "dia" | "semana" | "mes";

/**
 * The fixed, closed set of reasons a thumbs-down vote may carry, zero or more at once.
 * Matches `RazonesDeRetroalimentacion.Todas` on the backend exactly — see
 * `POST /api/asistente/retroalimentacion` in docs/architecture/api-contracts.md.
 * The retired `lento` (asistente-rediseno-v3, design.md D7, removed entirely
 * 2026-09-26) is deliberately absent: the client never sends it, and the server
 * rejects it with `400` like any other unknown value.
 */
export type RazonDeRetroalimentacion =
  "datos_incorrectos" | "no_entendio_la_pregunta" | "faltan_datos" | "otro";

// ============================================================
// Menciones «@materia» / «#docente» del composer (asistente-menciones,
// design.md D10/D11 de asistente-rediseno-v3). Ver
// docs/architecture/api-contracts.md §Asistente — Menciones.
// ============================================================

/** El disparador elige el tipo: no hay una tercera cosa que mencionar. */
export type TipoDeMencion = "materia" | "docente";

/** Lo que viaja en `POST /consultas`: sólo el tipo y el id, nunca el texto. */
export interface ReferenciaDeMencion {
  tipo: TipoDeMencion;
  id: string;
}

/** Una fila de `GET /api/asistente/menciones`. */
export interface ResultadoDeMencion {
  id: string;
  nombre: string;
  /** Sólo en una materia. */
  carrera?: string | null;
  /** Sólo en una materia. */
  codigo?: string | null;
  /** Sólo en un docente. */
  cargo?: string | null;
}

/** La respuesta completa de `GET /api/asistente/menciones`. */
export interface BusquedaDeMenciones {
  resultados: ResultadoDeMencion[];
  /** Nunca un conteo — mismo motivo que `RespuestaDelAsistente.truncado`. */
  hayMas: boolean;
}

/**
 * Una mención elegida en el popover, mientras se sigue escribiendo el
 * borrador: la referencia que viajaría, más el texto —disparador y nombre—
 * que quedó insertado en el campo. `referenciasVigentes` (utils/menciones.ts)
 * la filtra contra el borrador actual al enviar: si el texto ya no está, la
 * referencia no viaja (asistente-menciones, «Deleting the mention text drops
 * its reference»).
 */
export interface ChipDeMencion extends ReferenciaDeMencion {
  texto: string;
}

/**
 * Una mención ya ubicada en el texto de una pregunta YA ENVIADA, con la
 * posición donde `Mensaje` la reemplaza por un chip. Se congela al enviar:
 * la pregunta de un turno no vuelve a cambiar, así que la posición tampoco.
 */
export interface MencionEnPregunta extends ReferenciaDeMencion {
  texto: string;
  inicio: number;
  fin: number;
}

/** Un turno ya renderizable, del lado del cliente. */
export interface TurnoDeLaConversacion {
  id: string;
  pregunta: string;
  /**
   * Las menciones de ESTA pregunta, ya ubicadas en su texto (asistente-
   * menciones): `Mensaje` las pinta como chips. Ausente si no se elegió
   * ninguna, o si ninguna sobrevivió hasta el envío.
   */
  menciones?: MencionEnPregunta[];
  /** Ausente mientras el turno está en vuelo. */
  respuesta?: RespuestaDelAsistente;
  /** Mensaje comprensible cuando el pedido falló por transporte. */
  error?: string;
  /** El usuario dejó de esperarlo: el request se soltó de este lado. No es un error. */
  detenido?: boolean;
  /**
   * Presente sólo en un turno restaurado de una conversación reanudada
   * (asistente-historial-conversaciones). El texto redactado nunca se
   * persiste (design.md D2/D4), así que este turno no tiene `respuesta`: en
   * su lugar se muestra su desenlace y, si terminó `respondida`, la acción
   * «volver a consultar».
   */
  historico?: TurnoHistoricoEnCurso;
  /**
   * El identificador del turno que ESTE turno reemplaza —su propio `id`
   * anterior—, si nació de «Editar y reenviar»
   * (asistente-edicion-de-la-ultima-pregunta). Se reusa en «Reintentar»,
   * para que un reenvío fallido reintente con el mismo objetivo.
   */
  reemplaza?: string;
}

// ============================================================
// El historial de conversaciones propias, y su lectura de soporte.
// Ver docs/architecture/api-contracts.md §Asistente — Historial.
// ============================================================

/** Una conversación propia, en la lista. */
export interface ConversacionResumen {
  id: string;
  titulo: string;
  creadoEn: string;
  ultimaActividad: string;
  /** design.md D3 de asistente-rediseno-v3. Archivar no toca `ultimaActividad`. */
  archivada: boolean;
  /**
   * design.md D4. Ausente/falso en el propio historial —nunca lista una
   * conversación pendiente—; presente en verdadero SÓLO en la lectura de
   * soporte (asistente-acceso-de-soporte-al-historial), mientras la ventana
   * de «Deshacer» de esa conversación no venció.
   */
  pendienteDeBorrado?: boolean;
}

/** Lo que devuelve un borrado — uno o todos —: el lote, para poder deshacerlo. */
export interface LoteDeBorrado {
  loteDeBorrado: string;
}

/** Un turno de una conversación propia, tal como lo devuelve el historial. */
export interface TurnoDeHistorial {
  id: string;
  pregunta: string;
  /** Sólo con `asistente.ver_consulta` (propio) o siempre (lectura de soporte). */
  sql?: string | null;
  estado: EstadoDelTurno;
  ocurrioEn: string;
}

/** Una conversación propia, con sus turnos. */
export interface ConversacionDetalle extends ConversacionResumen {
  turnos: TurnoDeHistorial[];
}

/** Lo que devuelve reanudar una conversación propia. */
export interface ReanudarRespuesta {
  /** El id efímero NUEVO: el mismo campo `hilo` de `POST /consultas`. */
  hilo: string;
  turnos: TurnoDeHistorial[];
}

/** Lo que devuelve «volver a consultar» un turno propio ya respondido. */
export interface ReejecucionResultado {
  exitosa: boolean;
  /** Presente sólo cuando `exitosa` es falso: nunca un error crudo (design.md D4). */
  mensaje?: string | null;
  columnas: ColumnaDelResultado[];
  filas: unknown[][];
  truncado: boolean;
}

/** El estado, del lado del cliente, de un turno histórico restaurado. */
export interface TurnoHistoricoEnCurso {
  estado: EstadoDelTurno;
  sql: string | null;
  ocurrioEn: string;
  /** «Volver a consultar» en vuelo para este turno. */
  reejecutando?: boolean;
  /** El último resultado de «volver a consultar», si se pidió. */
  reejecucion?: ReejecucionResultado;
}
