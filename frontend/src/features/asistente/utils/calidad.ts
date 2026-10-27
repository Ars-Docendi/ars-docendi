import type { ConteoDeCalidad, MotivoSinTendencia, PuntoDeCalidad, TipoDeProblema } from "../types";

export const PERMISO_REVISAR_CALIDAD = "asistente.revisar_calidad";

export const PERIODOS_DE_CALIDAD = [
  { valor: 7, etiqueta: "7 días" },
  { valor: 30, etiqueta: "30 días" },
  { valor: 90, etiqueta: "90 días" },
] as const;

/** Reason codes of a negative vote, plus the bucket for a vote without reasons. */
const ETIQUETAS_DE_MOTIVO: Record<string, string> = {
  datos_incorrectos: "Datos incorrectos",
  faltan_datos: "Faltan datos",
  no_entendio_la_pregunta: "No entendió la pregunta",
  otro: "Otro",
  sin_motivo: "Sin motivo (omitió)",
};

/** The 22 cause codes of design D2 plus the unclassified bucket. */
const ETIQUETAS_DE_CAUSA: Record<string, string> = {
  proveedor_no_disponible: "Proveedor no disponible",
  proveedor_saturado: "Proveedor ocupado (no es una caída)",
  timeout_proveedor: "El modelo tardó demasiado",
  credencial_rechazada: "El proveedor rechazó la clave",
  modelo_no_encontrado: "Modelo no encontrado en el proveedor",
  error_del_proveedor: "Error del proveedor",
  techo_de_llamadas: "Tope de llamadas al modelo por turno",
  presupuesto_del_turno: "Se agotó el tiempo del turno",
  generacion_cortada: "Respuesta cortada por el límite de tokens",
  respuesta_ininteligible: "Respuesta del modelo ininteligible",
  validador_rechazo: "El validador rechazó el SQL",
  plan_invalido: "Plan de consulta inválido",
  sin_privilegio_de_datos: "Sin acceso a los datos",
  error_del_motor: "Error de la base al consultar",
  timeout_de_sentencia: "La consulta tardó demasiado en la base",
  excepcion: "Error inesperado",
  resultado_vacio: "Consulta sin resultados",
  cupo_agotado: "Cupo diario agotado",
  tope_organizacional: "Tope organizacional alcanzado",
  mantenimiento: "Modo mantenimiento",
  acceso_revocado: "Acceso revocado",
  turno_concurrente: "Otra pregunta en curso",
  sin_clasificar: "Sin clasificar",
};

export const ETIQUETAS_DE_TIPO: Record<TipoDeProblema, string> = {
  negativa: "Pulgar abajo",
  fallo: "Fallo",
  reemplazo: "Reemplazada",
};

export function etiquetaDeMotivo(clave: string): string {
  return ETIQUETAS_DE_MOTIVO[clave] ?? clave;
}

export function etiquetaDeCausa(clave: string): string {
  return ETIQUETAS_DE_CAUSA[clave] ?? clave;
}

/** `resultado_vacio` is information, not a failure: the screen marks it as a signal. */
export function esSenal(conteo: ConteoDeCalidad): boolean {
  return conteo.familia === "senal" || conteo.clave === "resultado_vacio";
}

/**
 * The detail of a problem row: a negative vote carries comma-separated reasons,
 * a failure carries a cause code, and a replacement carries nothing.
 */
export function detalleDeProblema(tipo: TipoDeProblema, motivo: string | null): string {
  if (tipo === "reemplazo") return "El usuario editó y reenvió la pregunta";
  if (!motivo) return tipo === "negativa" ? etiquetaDeMotivo("sin_motivo") : "—";
  if (tipo === "negativa") {
    return motivo
      .split(",")
      .map((m) => etiquetaDeMotivo(m.trim()))
      .join(", ");
  }
  return etiquetaDeCausa(motivo);
}

const UNO_DECIMAL = { minimumFractionDigits: 1, maximumFractionDigits: 1 } as const;

/** A fraction (0–1) as «12,3%»; `null` (no denominator) as a dash, never as 0. */
export function formatearTasa(fraccion: number | null | undefined): string {
  if (fraccion === null || fraccion === undefined) return "—";
  return `${(fraccion * 100).toLocaleString("es-AR", UNO_DECIMAL)}%`;
}

/** Fraction points as «2,3» (absolute value). */
export function formatearPuntos(fraccion: number): string {
  return Math.abs(fraccion * 100).toLocaleString("es-AR", UNO_DECIMAL);
}

export function formatearCantidad(n: number): string {
  return n.toLocaleString("es-AR");
}

/** «AAAA-MM-DD» as «dd/mm», without going through `Date` (no time-zone shift). */
export function formatearDia(dia: string): string {
  const [, mes, d] = dia.split("-");
  return `${d}/${mes}`;
}

export const TEXTO_SIN_TENDENCIA: Record<MotivoSinTendencia, string> = {
  pocos_votos: "Pocas valoraciones para comparar",
  fuera_de_retencion: "El período anterior ya no se conserva",
};

/** The bar's text: «24/10: 12,0% (3 de 25)», or «Semana del …» in a weekly series. */
export function textoDeBarra(punto: PuntoDeCalidad, porSemana: boolean): string {
  const dia = `${porSemana ? "Semana del " : ""}${formatearDia(punto.dia)}`;
  if (punto.votos === 0) return `${dia}: sin valoraciones`;
  return `${dia}: ${formatearTasa(punto.tasaNegativa)} (${punto.negativos} de ${punto.votos})`;
}

/** The top of the chart's scale: a multiple of 5 points, never below 5. */
export function maximoDeEscala(serie: PuntoDeCalidad[]): number {
  const mayor = Math.max(0, ...serie.map((p) => (p.tasaNegativa ?? 0) * 100));
  return Math.max(5, Math.ceil(mayor / 5) * 5);
}
