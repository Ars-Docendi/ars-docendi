/**
 * Lo que muestra una celda vacía. Exportada para que quien ordene por lo
 * mostrado (`ordenarFilas`) reconozca el mismo vacío sin duplicar el
 * carácter: hay una sola fuente de verdad de «cómo se ve una celda».
 */
export const CELDA_VACIA = "—";

/**
 * Una celda del resultado como texto, igual en la tabla y en lo que se copia de
 * ella: lo que el usuario ve es lo que se lleva.
 */
export function formatearCelda(valor: unknown): string {
  if (valor === null || valor === undefined) return CELDA_VACIA;
  if (typeof valor === "boolean") return valor ? "Sí" : "No";
  return String(valor);
}
