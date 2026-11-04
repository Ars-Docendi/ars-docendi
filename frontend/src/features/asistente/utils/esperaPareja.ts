/**
 * Cuánto retener una respuesta que no pasó por el modelo, para que el turno se sienta igual que
 * uno que sí pasó.
 */

/** Cuántos turnos con modelo entran en la media. */
const MUESTRAS = 5;

/** Media de arranque, hasta que haya un turno con modelo del cual aprender. */
export const MEDIA_SEMILLA_MS = 2600;

/**
 * Piso de la espera. NO ES ARBITRARIO: `IndicadorDeProceso` aparece a los 400 ms, y un indicador
 * que aparece y desaparece antes de terminar de leerse es peor que no mostrarlo —es el parpadeo
 * que ese umbral existe para evitar—.
 */
export const ESPERA_MINIMA_MS = 1000;

/**
 * Techo de la espera. Sin él, un día lento del proveedor —una media de ocho segundos— haría que
 * responder «hola» tardara ocho segundos.
 */
export const ESPERA_MAXIMA_MS = 2500;

/** Banda de la media que se usa como objetivo, antes de acotar. */
const FRACCION_MINIMA = 0.4;
const FRACCION_MAXIMA = 0.7;

export interface MedidorDeEspera {
  /** Anota lo que tardó un turno que SÍ llamó al modelo. */
  anotar: (duracionMs: number) => void;
  /** Cuánto debería durar, en total, un turno que no llamó al modelo. */
  objetivoMs: () => number;
}

/**
 * @param azar Inyectable: sin esto el objetivo no se puede afirmar en un test.
 */
export function crearMedidorDeEspera(azar: () => number = Math.random): MedidorDeEspera {
  const muestras: number[] = [];

  return {
    anotar(duracionMs) {
      // Una duración absurda —el reloj del sistema saltó, la pestaña estuvo
      // dormida— envenenaría la media durante los cinco turnos siguientes.
      if (!Number.isFinite(duracionMs) || duracionMs <= 0) return;
      muestras.push(duracionMs);
      if (muestras.length > MUESTRAS) muestras.shift();
    },

    objetivoMs() {
      const media =
        muestras.length === 0
          ? MEDIA_SEMILLA_MS
          : muestras.reduce((suma, uno) => suma + uno, 0) / muestras.length;

      // La fracción se sortea en cada turno: una espera clavada siempre en el mismo
      // número se percibe como un temporizador, no como trabajo.
      const fraccion = FRACCION_MINIMA + azar() * (FRACCION_MAXIMA - FRACCION_MINIMA);

      return Math.min(ESPERA_MAXIMA_MS, Math.max(ESPERA_MINIMA_MS, Math.round(media * fraccion)));
    },
  };
}

/** Espera lo que falte, o vuelve ya si no falta nada o si se dejó de esperar. */
export function esperarHasta(restanteMs: number, signal: AbortSignal): Promise<void> {
  if (restanteMs <= 0 || signal.aborted) return Promise.resolve();

  return new Promise((resolver) => {
    const temporizador = window.setTimeout(terminar, restanteMs);

    function terminar() {
      window.clearTimeout(temporizador);
      signal.removeEventListener("abort", terminar);
      resolver();
    }

    signal.addEventListener("abort", terminar, { once: true });
  });
}
