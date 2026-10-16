/**
 * Formato compartido del panel de administración del asistente
 * (asistente-administracion-de-uso): antes vivía sólo dentro de `PanelDeUso`,
 * ahora también lo usan `KpisDeUso` y `TopeOrganizacionalCard` — una sola
 * fuente de verdad para «cómo se ve un monto en USD» en toda esta pantalla.
 */

/** «US$ 12,34»: la factura del proveedor es la fuente de verdad, esto es sólo una guía. */
export function formatearUsd(valor: number): string {
  return new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency: "USD",
    minimumFractionDigits: 2,
  }).format(valor);
}

/** «1.234»: turnos, llamadas — siempre un entero, sin decimales. */
export function formatearEntero(valor: number): string {
  return new Intl.NumberFormat("es-AR").format(Math.round(valor));
}

/** «1,2 s»: latencias, que el backend manda en milisegundos. */
export function formatearLatenciaMs(valorMs: number): string {
  return (
    (valorMs / 1000).toLocaleString("es-AR", {
      minimumFractionDigits: 1,
      maximumFractionDigits: 1,
    }) + " s"
  );
}
