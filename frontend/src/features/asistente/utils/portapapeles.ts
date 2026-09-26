import { formatearCelda } from "./celdas";
import type { ColumnaDelResultado } from "../types";

/**
 * La tabla de resultados como texto tabulado: una fila de cabecera con los
 * nombres de las columnas y una fila por resultado, para pegar en una planilla.
 *
 * Un tabulador o un salto de línea dentro de una celda romperían la grilla al
 * pegarla, así que se reemplazan por un espacio.
 */
export function tablaComoTsv(columnas: ColumnaDelResultado[], filas: unknown[][]): string {
  const cabecera = columnas.map((columna) => celda(columna.nombre));
  const cuerpo = filas.map((fila) => fila.map((valor) => celda(formatearCelda(valor))));

  return [cabecera, ...cuerpo].map((fila) => fila.join("\t")).join("\n");
}

function celda(texto: string): string {
  return texto.replace(/[\t\r\n]+/g, " ");
}

// ============================================================
// CSV export: same source data as tablaComoTsv, but built for opening in a
// real spreadsheet application rather than pasting into one already open.
// Guards against CSV/formula injection, adds a UTF-8 BOM for Excel, and
// discloses truncation inside the file rather than only in its name.
// ============================================================

/** Leading byte-order mark: the standard fix for Excel (Windows) misreading UTF-8. */
const MARCA_DE_ORDEN_DE_BYTES = "﻿";

/**
 * Cell-starting characters that a spreadsheet application could read as the
 * start of a formula, including a tab or carriage return (OWASP's CSV
 * injection guidance) and a plain minus sign — a negative number is
 * neutralized the same way as any other leading-dash cell (see
 * asistente-exportacion-csv's spec: uniform treatment, no special-casing
 * "looks numeric").
 */
const PRIMEROS_CARACTERES_DE_FORMULA = new Set(["=", "+", "-", "@", "\t", "\r"]);

/**
 * Prefixes a cell with a single leading apostrophe if its first character
 * could run as a spreadsheet formula. Applied BEFORE RFC 4180 quoting, so the
 * apostrophe itself never needs escaping.
 */
function neutralizarFormula(texto: string): string {
  return texto.length > 0 && PRIMEROS_CARACTERES_DE_FORMULA.has(texto[0]) ? `'${texto}` : texto;
}

/** Wraps in double quotes (doubling internal ones) whenever RFC 4180 requires it. */
function celdaCsv(texto: string): string {
  const neutralizado = neutralizarFormula(texto);
  return /[",\r\n]/.test(neutralizado) ? `"${neutralizado.replaceAll('"', '""')}"` : neutralizado;
}

/** The trailing row a truncated export gets. States the fact, never a count. */
const AVISO_DE_TRUNCAMIENTO =
  "Resultado truncado: se muestran menos filas de las que cumplen la consulta.";

/**
 * The result table as a real, spreadsheet-safe `.csv` file: RFC 4180 quoting,
 * formula-injection guarding, a UTF-8 BOM, and CRLF line endings.
 *
 * Built entirely from the values already rendered — the same array
 * `TablaDeResultado` paints, already masked by the time it reaches this
 * function — never from any value re-fetched or unmasked. There is nothing
 * else client-side it could read even if it tried.
 */
export function tablaComoCsv(
  columnas: ColumnaDelResultado[],
  filas: unknown[][],
  truncado: boolean,
): string {
  const cabecera = columnas.map((columna) => celdaCsv(columna.nombre));
  const cuerpo = filas.map((fila) => fila.map((valor) => celdaCsv(formatearCelda(valor))));
  const lineas = truncado
    ? [cabecera, ...cuerpo, [celdaCsv(AVISO_DE_TRUNCAMIENTO)]]
    : [cabecera, ...cuerpo];

  return MARCA_DE_ORDEN_DE_BYTES + lineas.map((fila) => fila.join(",")).join("\r\n");
}

/**
 * Names the exported file: only ASCII-safe characters, and a truncation
 * marker when the result was truncated — a secondary, glanceable signal next
 * to the trailing row inside the file, never the only one.
 */
export function nombreDelArchivoCsv(hilo: string, fecha: Date, truncado: boolean): string {
  const hiloCorto = hilo.split("-")[0] || "sin-hilo";
  const fechaIso = fecha.toISOString().slice(0, 10);
  const sufijo = truncado ? "-truncado" : "";

  return `asistente-resultado-${hiloCorto}-${fechaIso}${sufijo}.csv`;
}

/** Si este navegador, en este contexto, tiene portapapeles. Sin TLS no lo hay. */
export function hayPortapapeles(): boolean {
  return typeof navigator !== "undefined" && navigator.clipboard !== undefined;
}

/** Deja el texto en el portapapeles. Rechaza si el navegador no lo permite. */
export function copiar(texto: string): Promise<void> {
  return navigator.clipboard.writeText(texto);
}
