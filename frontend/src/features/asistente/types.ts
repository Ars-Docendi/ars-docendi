// ============================================================
// El contrato de POST /api/asistente/consultas y GET /api/asistente/capacidades.
// Ver docs/architecture/api-contracts.md §Asistente.
// ============================================================

/** Los cuatro estados en que puede terminar un turno. */
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

/** Una celda que identifica algo que el usuario puede abrir. */
export interface VinculoDelResultado {
  fila: number;
  columna: number;
  tipo: string;
  id: string;
}

export interface MetricasDelTurno {
  llamadasAlModelo: number;
  // El backend también manda `categoria` («consulta_simple», «cruce_de_tablas»…) y acá no se
  // declara A PROPÓSITO: es la etiqueta interna del carril que resolvió el turno, y RNF-18 prohíbe
  // mostrar etiquetas internas.
}

/**
 * Una aparición, dentro de `respuesta`, de un valor de celda que lleva a una pantalla del sistema.
 */
export interface VinculoDelTexto {
  desde: number;
  largo: number;
  tipo: string;
  id: string;
}

/** El elemento del que trata la respuesta cuando el resultado es uno solo. */
export interface PrincipalDeLaRespuesta {
  tipo: string;
  id: string;
  datos: Record<string, string>;
}

export interface RespuestaDelAsistente {
  estado: EstadoDelTurno;
  respuesta: string;
  hilo: string;
  preguntaInterpretada?: string | null;
  razonamiento?: string | null;
  /** Bloquean el turno: hay que elegir una para seguir. */
  opciones: OpcionDeAclaracion[];
  columnas: ColumnaDelResultado[];
  filas: unknown[][];
  /** Booleano y nunca un conteo: cuántas filas faltan es un canal de inferencia. */
  truncado: boolean;
  /** Las celdas que llevan a una pantalla del sistema. Vacío si ninguna. */
  vinculos: VinculoDelResultado[];
  /** Dónde, dentro de `respuesta`, se nombran elementos vinculados. */
  vinculosDelTexto: VinculoDelTexto[];
  /** El elemento principal de la respuesta, o `null` si no hay uno solo. */
  principal: PrincipalDeLaRespuesta | null;
  /** Códigos de las áreas de datos consultadas (`designaciones`, `identity`, `portal`). */
  origenes: string[];
  /** Solo llega con el permiso `asistente.ver_consulta`. */
  sql?: string | null;
  metricas: MetricasDelTurno;
  /** Present only when `estado` is `"respondida"`. */
  claveDeRetroalimentacion?: string | null;
  /**
   * El cupo diario del actor INMEDIATAMENTE DESPUÉS de que este turno se cobrara
   * (asistente-cupo-visible) — nunca el valor de antes.
   */
  cupoRestante?: number | null;
  /**
   * El id de `GET /historial` en el que quedó este turno (design.md D13 de asistente-rediseno-v3).
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

/** Uno de los motivos por los que un turno puede estar bloqueado. */
export type MotivoDeBloqueo =
  "presupuesto_propio" | "tope_organizacional" | "mantenimiento" | "sin_acceso";

/**
 * El cupo diario de ESTE actor, con el bypass de mantenimiento del admin ya aplicado del lado del
 * backend (a diferencia de `mantenimiento`, que es global y sin bypass).
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
  /** Por qué cosas suele venir a preguntar este usuario, según su rol. */
  presentacion: string;
  mantenimiento: MantenimientoDelAsistente;
  cupo: CupoDelActor;
  /**
   * Si el turno se pide a `POST /consultas/flujo` para ver la redacción mientras se escribe
   * (asistente-optimizaciones-modelo-local, D9).
   */
  redaccionEnFlujo?: boolean;
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
  /**
   * Sólo en los agregados por usuario: los códigos de rol de sistema vigentes del actor (resuelto
   * vía `IConsultasIdentity`, sin nombre legible — el backend no lo tiene, `etiquetasDeRol` lo traduce acá).
   */
  codigosDeRol: string[];
  /**
   * Sólo en los agregados por usuario: el cupo diario que hoy le aplicaría la ejecución real del
   * turno a este actor —la misma regla que aplica el backend al bloquear un turno—, o `null` si no
   * se puede resolver (el actor no tiene override ni ningún rol de sistema vigente).
   */
  cupoEfectivo: number | null;
  /** De dónde sale `cupoEfectivo`: `"override"` o `"rol"`, o `null` junto con `cupoEfectivo` nulo. */
  origenDeCupo: "override" | "rol" | null;
  /**
   * Sólo en los agregados por usuario: si el actor tiene hoy acceso al asistente, con la misma
   * regla que aplica el turno (asistente-acceso-granular).
   */
  accesoEfectivo: boolean | null;
  /** De dónde sale `accesoEfectivo`: heredado del `"rol"` o una revocación `"propio"`. */
  origenDeAcceso: "rol" | "propio" | null;
}

/** El panel de uso completo (`GET /api/asistente/administracion/uso`). */
export interface UsoDelAsistente {
  porUsuario: UsoAgregado[];
  porRol: UsoAgregado[];
  organizacion: UsoAgregado;
}

/** Período del panel de uso: relativo (`día`/`semana`/`mes`) o un rango explícito. */
export type PeriodoDeUso = "dia" | "semana" | "mes";

// ============================================================
// Estado persistido de los presupuestos (asistente-administracion-de-uso,
// tarea 12.8 de sistema-seccion-unificada). Ver
// docs/architecture/api-contracts.md §GET /api/asistente/administracion/presupuestos.
// ============================================================

/** El cupo diario default de UN rol, tal como está persistido. */
export interface CupoDeRolPersistido {
  rol: string;
  cupoDiarioTurnos: number;
  /** El acceso operativo del rol al asistente (asistente-acceso-granular). */
  accesoHabilitado: boolean;
}

/**
 * La carga del servidor del modelo propio y de la compuerta del backend (`GET
 * /api/asistente/administracion/servidor-local`, asistente-optimizaciones-modelo-local D8).
 */
export interface ServidorLocal {
  configurado: boolean;
  alcanzable: boolean;
  motor: "vllm" | "llama.cpp" | null;
  enCurso: number | null;
  enEspera: number | null;
  /** De 0 a 1. */
  usoDeKvCache: number | null;
  /** De 0 a 1, acumulado desde que arrancó el servidor. Sólo vLLM la publica. */
  aciertosDeCacheDePrefijo: number | null;
  compuerta: { capacidad: number; enCurso: number; enEspera: number } | null;
}

/** El override de cupo diario de un usuario puntual, tal como está persistido. */
export interface OverrideDeUsuarioPersistido {
  actorId: string;
  /** Resuelto vía `IConsultasIdentity`, igual que `UsoAgregado.nombreParaMostrar`. */
  nombreParaMostrar?: string | null;
  cupoDiarioTurnos: number;
}

/**
 * El estado persistido de los presupuestos (`GET /api/asistente/administracion/presupuestos`):
 * tope, cupos, acceso por rol y revocaciones por usuario (asistente-acceso-granular).
 */
export interface PresupuestosDelAsistente {
  /** El tope organizacional vigente. `0` = desactivado. */
  topeMensualUsd: number;
  /**
   * El costo estimado del mes calendario en curso (límite UTC), calculado
   * con el mismo mecanismo de costeo versionado que `GET …/uso`.
   */
  gastoEstimadoDelMes: number;
  /** Siempre `true`: nunca la factura real del proveedor. */
  esEstimado: boolean;
  cuposPorRol: CupoDeRolPersistido[];
  overridesPorUsuario: OverrideDeUsuarioPersistido[];
  /** Los actores con el acceso revocado por un administrador. */
  accesosRevocados: string[];
}

/** The fixed, closed set of reasons a thumbs-down vote may carry, zero or more at once. */
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
 * Una mención elegida en el popover, mientras se sigue escribiendo el borrador: la referencia que
 * viajaría, más el texto —disparador y nombre— que quedó insertado en el campo.
 */
export interface ChipDeMencion extends ReferenciaDeMencion {
  texto: string;
}

/**
 * Una mención ya ubicada en el texto de una pregunta YA ENVIADA, con la posición donde `Mensaje`
 * la reemplaza por un chip.
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
   * Las menciones de ESTA pregunta, ya ubicadas en su texto (asistente- menciones): `Mensaje` las
   * pinta como chips.
   */
  menciones?: MencionEnPregunta[];
  /** Ausente mientras el turno está en vuelo. */
  respuesta?: RespuestaDelAsistente;
  /**
   * Lo que llegó de la redacción mientras el turno sigue en vuelo, con la redacción por fragmentos
   * (asistente-optimizaciones-modelo-local, D9).
   */
  redaccionParcial?: string;
  /** Mensaje comprensible cuando el pedido falló por transporte. */
  error?: string;
  /** El usuario dejó de esperarlo: el request se soltó de este lado. No es un error. */
  detenido?: boolean;
  /**
   * Presente sólo en un turno restaurado de una conversación reanudada
   * (asistente-historial-conversaciones).
   */
  historico?: TurnoHistoricoEnCurso;
  /**
   * El identificador del turno que ESTE turno reemplaza —su propio `id` anterior—, si nació de
   * «Editar y reenviar» (asistente-edicion-de-la-ultima-pregunta).
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

/**
 * Una mención de un turno histórico, ya re-resuelta por el backend para EL ACTOR QUE LEE ahora
 * (design.md D11 de asistente-rediseno-v3, decisión 15 del PO): `etiqueta` es el texto exacto que
 * el composer insertó al elegirla («@Nombre» / «#Nombre»), así que `sembrarDesdeHistorial`
 * (`useAsistente.ts`) la ubica en el texto de la pregunta reusando `ubicarMenciones`, sin que el
 * backend calcule posiciones.
 */
export interface MencionDeHistorial {
  tipo: TipoDeMencion;
  id: string;
  etiqueta: string;
}

/** Un turno de una conversación propia, tal como lo devuelve el historial. */
export interface TurnoDeHistorial {
  id: string;
  pregunta: string;
  /** Sólo con `asistente.ver_consulta` (propio) o siempre (lectura de soporte). */
  sql?: string | null;
  estado: EstadoDelTurno;
  ocurrioEn: string;
  /**
   * AUSENTE en la lectura de soporte (nunca es el actor cuyo alcance decide la visibilidad); en el
   * lado propio siempre viaja, aunque quede vacío si ninguna referencia sobrevivió la revalidación.
   */
  menciones?: MencionDeHistorial[];
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

// ============================================================
// Modelo y claves de proveedor (asistente-modelo-en-caliente, slice 3).
// Ver docs/architecture/api-contracts.md §«Modelo y claves de proveedor».
//
// Ningún tipo de RESPUESTA tiene un campo capaz de llevar una clave: sólo la
// pista de cuatro caracteres. La clave existe únicamente en `PedidoDeModelo` y
// `claveNueva`, que viajan de la pantalla al servidor y no vuelven.
// ============================================================

/** `servidor` es la configuración del ambiente; `pantalla`, una selección guardada. */
export type OrigenDelModelo = "servidor" | "pantalla";

export interface ModeloEnUso {
  origen: OrigenDelModelo;
  empresa: string;
  modelo: string;
}

export interface CredencialDeEmpresa {
  estado: "guardada" | "faltante";
  /** Los últimos cuatro caracteres; `null` cuando falta la clave. */
  pista: string | null;
}

/** Un grupo del catálogo. Los grupos `local` no llevan credencial. */
export interface EmpresaDelCatalogo {
  empresa: string;
  tipo: "nube" | "local";
  credencial: CredencialDeEmpresa | null;
  modelos: string[];
}

export interface EstadoDelModelo {
  enUso: ModeloEnUso;
  /** Identificadores (`ollama`, `local`) de los servidores configurados; nunca una URL. */
  servidoresLocales: string[];
  catalogo: EmpresaDelCatalogo[];
  /** `false` cuando falta la clave maestra: guardar claves no está disponible. */
  cifradoDisponible: boolean;
  /** El precio vigente ahora de cada modelo del catálogo que tiene uno. */
  precios: PrecioDelModelo[];
}

/** Un precio vigente, en US$ por millón de tokens. */
export interface PrecioDelModelo {
  empresa: string;
  modelo: string;
  entradaPorMillon: number;
  salidaPorMillon: number;
  cachePorMillon: number;
  version: number;
  /** Fecha de inicio, «AAAA-MM-DD». */
  vigenteDesde: string;
}

/** Un precio nuevo para un modelo del catálogo, en US$ por millón de tokens. */
export interface PedidoDePrecio {
  empresa: string;
  modelo: string;
  entradaPorMillon: number;
  salidaPorMillon: number;
  cachePorMillon?: number;
  /** «AAAA-MM-DD». */
  vigenteDesde: string;
}

export interface PedidoDeModelo {
  empresa: string;
  modelo: string;
  /** Una clave todavía no guardada, de una empresa en la nube. */
  claveNueva?: string;
}

/** `exito: false` es una respuesta 200: la prueba corrió y el proveedor no la aceptó. */
export interface ResultadoDePrueba {
  exito: boolean;
  latenciaMs: number | null;
  codigo: string | null;
}

export interface ModeloAplicado {
  enUso: ModeloEnUso;
  latenciaMs: number;
  pista: string | null;
}

export interface CredencialReemplazada {
  empresa: string;
  pista: string;
}

// ---------- Calidad de las respuestas (asistente-metricas-de-calidad) ----------

export type PeriodoDeCalidad = 7 | 30 | 90;

export type TipoDeProblema = "negativa" | "fallo" | "reemplazo";

/** Fractions between 0 and 1, or `null` when the denominator is zero. */
export interface ResumenDeCalidad {
  turnos: number;
  votos: number;
  positivos: number;
  negativos: number;
  respondidas: number;
  conProblemas: number;
  fallosDetectables: number;
  tasaNegativa: number | null;
  tasaConProblemas: number | null;
  tasaDeFallos: number | null;
  cobertura: number | null;
}

export interface ConteoDeCalidad {
  clave: string;
  cantidad: number;
  /** `proveedor`, `consulta`, `senal` or `politica`; only for causes. */
  familia?: string | null;
}

export interface ModeloDeCalidad {
  modelo: string | null;
  resumen: ResumenDeCalidad;
}

export interface PuntoDeCalidad {
  /** «AAAA-MM-DD»: the day, or the Monday of the week when the series is weekly. */
  dia: string;
  turnos: number;
  votos: number;
  negativos: number;
  tasaNegativa: number | null;
}

export interface TendenciaDeCalidad {
  tasaAnterior: number;
  /** Current minus previous, in fraction points (0.05 is five points). */
  variacion: number;
  votosAnteriores: number;
}

export type MotivoSinTendencia = "pocos_votos" | "fuera_de_retencion";

export interface CalidadAgregada {
  periodoDias: number;
  modelo: string | null;
  desde: string;
  hasta: string;
  porSemana: boolean;
  resumen: ResumenDeCalidad;
  tendencia: TendenciaDeCalidad | null;
  motivoSinTendencia: MotivoSinTendencia | null;
  negativosPorMotivo: ConteoDeCalidad[];
  fallosPorCausa: ConteoDeCalidad[];
  denegacionesDePolitica: ConteoDeCalidad[];
  porModelo: ModeloDeCalidad[];
  modelosDisponibles: string[];
  serie: PuntoDeCalidad[];
}

export interface ProblemaDeCalidad {
  tipo: TipoDeProblema;
  pregunta: string;
  motivo: string | null;
  modelo: string | null;
  dia: string;
  estado: string;
}

export interface PaginaDeProblemas {
  pagina: number;
  tamanoDePagina: number;
  total: number;
  filas: ProblemaDeCalidad[];
}
