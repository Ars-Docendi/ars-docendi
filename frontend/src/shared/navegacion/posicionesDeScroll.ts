const CLAVE_ALMACEN = "ars-docendi:scroll";
export const posiciones = new Map<string, number>();

function leerAlmacen() {
  try {
    const crudo = sessionStorage.getItem(CLAVE_ALMACEN);
    if (crudo)
      for (const [k, v] of Object.entries(JSON.parse(crudo) as Record<string, number>))
        posiciones.set(k, v);
  } catch {
    // Sin almacenamiento: las posiciones viven mientras la pestaña esté abierta y no se recargue.
  }
}
let almacenLeido = false;

/** Lee el almacén de sesión la primera vez que se necesita. */
export function asegurarAlmacen() {
  if (almacenLeido) return;
  almacenLeido = true;
  leerAlmacen();
}

export function persistir() {
  try {
    sessionStorage.setItem(CLAVE_ALMACEN, JSON.stringify(Object.fromEntries(posiciones)));
  } catch {
    // Ídem.
  }
}

/** Sólo para tests. */
export function reiniciarPosicionesDeScroll() {
  posiciones.clear();
  almacenLeido = false;
  try {
    sessionStorage.removeItem(CLAVE_ALMACEN);
  } catch {
    // Ídem.
  }
}
