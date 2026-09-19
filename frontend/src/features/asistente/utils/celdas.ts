/**
 * Una celda del resultado como texto, igual en la tabla y en lo que se copia de
 * ella: lo que el usuario ve es lo que se lleva.
 */
export function formatearCelda(valor: unknown): string {
  if (valor === null || valor === undefined) return "—";
  if (typeof valor === "boolean") return valor ? "Sí" : "No";
  return String(valor);
}
