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

/** Si este navegador, en este contexto, tiene portapapeles. Sin TLS no lo hay. */
export function hayPortapapeles(): boolean {
  return typeof navigator !== "undefined" && navigator.clipboard !== undefined;
}

/** Deja el texto en el portapapeles. Rechaza si el navegador no lo permite. */
export function copiar(texto: string): Promise<void> {
  return navigator.clipboard.writeText(texto);
}
