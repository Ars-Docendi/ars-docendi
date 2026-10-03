/**
 * Fechas y horas en la zona horaria de la institución (design D11,
 * sistema-seccion-unificada): SIEMPRE `America/Argentina/Buenos_Aires`, sin
 * importar la zona del navegador. Todo pasa por `Intl.DateTimeFormat` con
 * `timeZone` explícito — nada de offsets fijos a mano — para que un futuro
 * cambio de regla de horario de verano no corra los filtros en silencio.
 */

export const ZONA_INSTITUCION = "America/Argentina/Buenos_Aires";
export type Periodo = "hoy" | "7d" | "30d" | "todo";

interface PartesFecha {
  anio: number;
  mes: number;
  dia: number;
  hora: number;
  minuto: number;
  segundo: number;
}

function partesEnZona(fecha: Date, zona: string): PartesFecha {
  const dtf = new Intl.DateTimeFormat("en-US", {
    timeZone: zona,
    hour12: false,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
  const partes = Object.fromEntries(dtf.formatToParts(fecha).map((p) => [p.type, p.value]));
  // Algunos motores devuelven "24" para la medianoche con hour12:false.
  const hora = partes.hour === "24" ? 0 : Number(partes.hour);
  return {
    anio: Number(partes.year),
    mes: Number(partes.month),
    dia: Number(partes.day),
    hora,
    minuto: Number(partes.minute),
    segundo: Number(partes.second),
  };
}

interface FechaSimple {
  anio: number;
  mes: number;
  dia: number;
}

function fechaSimpleEnZona(fecha: Date, zona: string): FechaSimple {
  const { anio, mes, dia } = partesEnZona(fecha, zona);
  return { anio, mes, dia };
}

function restarDiasCalendario({ anio, mes, dia }: FechaSimple, dias: number): FechaSimple {
  // Mediodía UTC: la aritmética de días queda libre de cualquier borde de huso.
  const resultado = new Date(Date.UTC(anio, mes - 1, dia, 12) - dias * 86_400_000);
  return {
    anio: resultado.getUTCFullYear(),
    mes: resultado.getUTCMonth() + 1,
    dia: resultado.getUTCDate(),
  };
}

function mismaFecha(a: FechaSimple, b: FechaSimple): boolean {
  return a.anio === b.anio && a.mes === b.mes && a.dia === b.dia;
}

function offsetMinutos(fechaAproximada: Date, zona: string): number {
  const p = partesEnZona(fechaAproximada, zona);
  const comoUtc = Date.UTC(p.anio, p.mes - 1, p.dia, p.hora, p.minuto, p.segundo);
  return (comoUtc - fechaAproximada.getTime()) / 60_000;
}

/**
 * Convierte una hora de pared (`datetime-local`, "AAAA-MM-DDTHH:mm") en la
 * zona indicada a un instante ISO, calculando el offset con `Intl` (no un
 * offset fijo) para que valga incluso si la regla de la zona cambia.
 */
export function datetimeLocalAIso(valorLocal: string, zona: string = ZONA_INSTITUCION): string {
  const [fechaParte, horaParte = "00:00"] = valorLocal.split("T");
  const [anio, mes, dia] = fechaParte.split("-").map(Number);
  const [hora, minuto] = horaParte.split(":").map(Number);
  const aproxUtc = Date.UTC(anio, mes - 1, dia, hora, minuto);
  const offset = offsetMinutos(new Date(aproxUtc), zona);
  return new Date(aproxUtc - offset * 60_000).toISOString();
}

function inicioDelDia(fechaSimple: FechaSimple, zona: string): string {
  const yyyy = String(fechaSimple.anio).padStart(4, "0");
  const mm = String(fechaSimple.mes).padStart(2, "0");
  const dd = String(fechaSimple.dia).padStart(2, "0");
  return datetimeLocalAIso(`${yyyy}-${mm}-${dd}T00:00`, zona);
}

/** El `desde` de cada período (design D11): `undefined` para «todo» (sin cota). */
export function limiteDesdeParaPeriodo(
  periodo: Periodo,
  ahora: Date = new Date(),
  zona: string = ZONA_INSTITUCION,
): string | undefined {
  if (periodo === "todo") return undefined;
  const diasAtras = periodo === "hoy" ? 0 : periodo === "7d" ? 6 : 29;
  const hoy = fechaSimpleEnZona(ahora, zona);
  const objetivo = diasAtras === 0 ? hoy : restarDiasCalendario(hoy, diasAtras);
  return inicioDelDia(objetivo, zona);
}

/** Clave de agrupación estable por día calendario en la zona (p. ej. "2026-09-26"). */
export function claveDiaEnZona(fecha: Date, zona: string = ZONA_INSTITUCION): string {
  const { anio, mes, dia } = fechaSimpleEnZona(fecha, zona);
  return `${anio}-${String(mes).padStart(2, "0")}-${String(dia).padStart(2, "0")}`;
}

const MESES = [
  "enero",
  "febrero",
  "marzo",
  "abril",
  "mayo",
  "junio",
  "julio",
  "agosto",
  "septiembre",
  "octubre",
  "noviembre",
  "diciembre",
];

function capitalizar(texto: string): string {
  return texto.charAt(0).toLocaleUpperCase("es-AR") + texto.slice(1);
}

function diaDeLaSemana(fecha: Date, zona: string): string {
  return new Intl.DateTimeFormat("es-AR", { timeZone: zona, weekday: "long" }).format(fecha);
}

/** «Hoy · viernes 26 de septiembre» / «Ayer · …» / «Martes 23 de septiembre» (design D11). */
export function tituloDiaEnZona(fecha: Date, ahora: Date, zona: string = ZONA_INSTITUCION): string {
  const simple = fechaSimpleEnZona(fecha, zona);
  const hoy = fechaSimpleEnZona(ahora, zona);
  const ayer = restarDiasCalendario(hoy, 1);
  const textoFecha = `${simple.dia} de ${MESES[simple.mes - 1]}`;
  const diaSemana = diaDeLaSemana(fecha, zona);

  if (mismaFecha(simple, hoy)) return `Hoy · ${diaSemana} ${textoFecha}`;
  if (mismaFecha(simple, ayer)) return `Ayer · ${diaSemana} ${textoFecha}`;
  return `${capitalizar(diaSemana)} ${textoFecha}`;
}

/** «Hoy HH:mm» / «Ayer HH:mm» / «d/m HH:mm» para «Cambios recientes» (design D11). */
export function horaCortaEnZona(fecha: Date, ahora: Date, zona: string = ZONA_INSTITUCION): string {
  const simple = fechaSimpleEnZona(fecha, zona);
  const { hora, minuto } = partesEnZona(fecha, zona);
  const hoy = fechaSimpleEnZona(ahora, zona);
  const ayer = restarDiasCalendario(hoy, 1);
  const hhmm = `${String(hora).padStart(2, "0")}:${String(minuto).padStart(2, "0")}`;

  if (mismaFecha(simple, hoy)) return `Hoy ${hhmm}`;
  if (mismaFecha(simple, ayer)) return `Ayer ${hhmm}`;
  return `${simple.dia}/${simple.mes} ${hhmm}`;
}

/** «22:10:04», para cada fila de la lista de auditoría. */
export function horaHmsEnZona(fecha: Date, zona: string = ZONA_INSTITUCION): string {
  const { hora, minuto, segundo } = partesEnZona(fecha, zona);
  return [hora, minuto, segundo].map((n) => String(n).padStart(2, "0")).join(":");
}

/** «26/9/2026 22:10:04», para el panel de detalle. */
export function fechaHoraCompletaEnZona(fecha: Date, zona: string = ZONA_INSTITUCION): string {
  const { anio, mes, dia, hora, minuto, segundo } = partesEnZona(fecha, zona);
  const hhmmss = [hora, minuto, segundo].map((n) => String(n).padStart(2, "0")).join(":");
  return `${dia}/${mes}/${anio} ${hhmmss}`;
}
