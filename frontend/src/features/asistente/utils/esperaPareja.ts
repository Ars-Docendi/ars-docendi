/**
 * Cuánto retener una respuesta que no pasó por el modelo, para que el turno se
 * sienta igual que uno que sí pasó.
 *
 * POR QUÉ EXISTE. Los carriles deterministas responden en milisegundos y el carril
 * SQL en segundos: un orden de magnitud de diferencia. Una respuesta instantánea
 * después de otra que tardó cinco segundos no se lee como «fue rápido», se lee como
 * «no hizo nada» o «no me entendió». La espera pareja es lo que hace que la
 * diferencia de carril deje de ser algo que el usuario tiene que interpretar.
 *
 * DE DÓNDE SALE EL NÚMERO. De los turnos reales de esta sesión, no de una constante:
 * el medidor anota cuánto tardó cada turno que sí llamó al modelo y saca la media de
 * los últimos. Así se adapta al proveedor, al modelo y a la red del día, que es
 * justo lo que una constante escrita a mano no puede hacer.
 *
 * QUÉ NO ES. No es progreso simulado por etapas: el indicador sigue diciendo una
 * sola cosa honesta —«Consultando…»— y no inventa pasos que no ocurren. Y el
 * retardo vive ACÁ, en el cliente: el backend responde tan rápido como puede y
 * `latencia_ms` del registro operativo sigue midiendo trabajo real. Un retardo del
 * lado del servidor habría corrompido la única métrica de latencia que tenemos.
 */

/** Cuántos turnos con modelo entran en la media. */
const MUESTRAS = 5;

/**
 * Media de arranque, hasta que haya un turno con modelo del cual aprender.
 *
 * El primer turno de una conversación suele ser un saludo, así que este número es
 * el que más se ve. Está deliberadamente por debajo de lo que tarda un turno con
 * modelo medido contra el proveedor real —del orden de cinco segundos—: mientras
 * no haya muestras conviene equivocarse esperando de menos.
 *
 * Y está por encima de lo que parece necesario a simple vista porque la banda se
 * calcula sobre él: con una semilla más chica, el piso se comería el sorteo y las
 * primeras esperas serían todas idénticas, que es justo lo que se percibe como
 * temporizador.
 */
export const MEDIA_SEMILLA_MS = 2600;

/**
 * Piso de la espera.
 *
 * NO ES ARBITRARIO: `IndicadorDeProceso` aparece a los 400 ms, y un indicador que
 * aparece y desaparece antes de terminar de leerse es peor que no mostrarlo —es el
 * parpadeo que ese umbral existe para evitar—. Con un piso de un segundo, cuando el
 * indicador aparece se queda al menos 600 ms en pantalla.
 */
export const ESPERA_MINIMA_MS = 1000;

/**
 * Techo de la espera.
 *
 * Sin él, un día lento del proveedor —una media de ocho segundos— haría que
 * responder «hola» tardara ocho segundos. La espera pareja existe para que el
 * usuario no tenga que interpretar la diferencia de carril, no para hacer el
 * producto más lento.
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

/**
 * Espera lo que falte, o vuelve ya si no falta nada o si se dejó de esperar.
 *
 * Escucha la señal para que «Dejar de esperar» libere el campo en el acto: sin eso,
 * el botón parecería no responder hasta que venciera el temporizador.
 */
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
