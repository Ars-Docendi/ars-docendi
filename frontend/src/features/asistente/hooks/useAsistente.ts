import { useCallback, useEffect, useRef, useState } from "react";

import { consultar } from "../api/asistenteApi";
import { reejecutarTurno } from "../api/historialApi";
import { esCancelacion, esHiloPerdido, mensajeDeError } from "../errores";
import type { TurnoDeHistorial, TurnoDeLaConversacion } from "../types";
import { crearMedidorDeEspera, esperarHasta } from "../utils/esperaPareja";

export interface Asistente {
  turnos: TurnoDeLaConversacion[];
  enVuelo: boolean;
  preguntar: (mensaje: string) => Promise<void>;
  /** Reenvía un turno que terminó en error, con su misma clave y su mismo texto. */
  reintentar: (id: string) => Promise<void>;
  /**
   * Edita y reenvía la última pregunta de la conversación
   * (asistente-edicion-de-la-ultima-pregunta): manda un turno nuevo, con una
   * clave nueva, que le pide al backend reemplazar el actual último turno.
   * El turno que resulta toma un `id` nuevo —nunca el que tenía—, así que su
   * `key` en la lista cambia y el voto, el orden y la vista ampliada de la
   * tabla se resetean solos al volver a montarse.
   */
  reenviarUltima: (texto: string) => Promise<void>;
  /** Vacía la conversación y descarta el hilo: la próxima pregunta arranca de cero. */
  reiniciar: () => void;
  /** Deja de esperar el turno en vuelo: suelta el request. El backend lo sigue igual. */
  detener: () => void;
  /**
   * Reanuda una conversación propia (asistente-historial-conversaciones,
   * design.md D3): reemplaza la conversación en curso por los turnos
   * restaurados y fija el hilo efímero nuevo, para que el próximo
   * `preguntar` siga esa misma conversación persistida.
   */
  sembrarDesdeHistorial: (hiloEfimero: string, turnos: TurnoDeHistorial[]) => void;
  /**
   * «Volver a consultar» (design.md D4): re-ejecuta la SQL guardada de un
   * turno histórico ya respondido, bajo el alcance actual del actor. Nunca
   * escribe una fila nueva de historial ni llama al modelo.
   */
  reejecutar: (id: string) => Promise<void>;
}

/** El turno en vuelo: su id y su request, para soltarlo desde afuera de su promesa. */
interface TurnoEnCurso {
  id: string;
  aborto: AbortController;
}

/**
 * La conversación de esta sesión.
 *
 * El hilo vive acá y no en un store global. El backend ya decidió no persistirlo, y
 * un store agregaría decisiones de ciclo de vida —cuándo se limpia, qué pasa al
 * cambiar de rol— para un estado que muere igual al recargar la página.
 *
 * LO INVOCA EL DUEÑO DEL MONTAJE —el lanzador de la barra— y no el panel. El
 * panel se desmonta al cerrar el modal, y con él se iba la conversación si viviera
 * ahí; el lanzador vive con la barra, así que al reabrir el hilo sigue donde
 * estaba.
 */
export function useAsistente(): Asistente {
  const [turnos, setTurnos] = useState<TurnoDeLaConversacion[]>([]);
  const [enVuelo, setEnVuelo] = useState(false);
  const hilo = useRef<string | null>(null);
  // Es también el guard de «un turno a la vez»: las funciones de abajo se memoizan
  // una sola vez, así que leer `enVuelo` adentro daría siempre el valor del primer
  // render.
  const enCurso = useRef<TurnoEnCurso | null>(null);
  const montado = useRef(true);
  // Aprende de los turnos que sí llamaron al modelo para saber cuánto retener los
  // que no. Vive en un ref porque es memoria de la sesión, no estado que se pinte.
  const medidor = useRef(crearMedidorDeEspera());

  // Quien se desmonta con un turno en vuelo se lleva el request consigo. Sin esto
  // el pedido sobrevive al componente y la respuesta cae sobre un estado que ya no
  // existe. Corre al desmontarse el DUEÑO del hook —el lanzador de la barra—, que
  // en la práctica sólo pasa si la aplicación entera se desmonta; cerrar el modal
  // no aborta nada, porque el lanzador sigue montado.
  useEffect(() => {
    montado.current = true;
    return () => {
      montado.current = false;
      enCurso.current?.aborto.abort();
    };
  }, []);

  // Un envío, sea el primero, un reintento o un reemplazo. Quien llama ya
  // puso el turno en la lista; acá se lo manda y se lo completa con lo que
  // vuelva. `reemplaza` es el identificador del turno que este envío
  // reemplaza —ausente en un turno nuevo cualquiera y en un reintento de
  // ÉSE—, para «Editar y reenviar».
  const enviar = useCallback(async (id: string, texto: string, reemplaza?: string) => {
    const aborto = new AbortController();
    enCurso.current = { id, aborto };
    setEnVuelo(true);

    const arranco = performance.now();

    try {
      const respuesta = await consultar({ mensaje: texto, hilo: hilo.current, reemplaza }, id, {
        signal: aborto.signal,
      });
      // Si mientras tanto se dejó de esperar o se reinició la conversación, lo que
      // llegue ya no es de nadie: tampoco el hilo, que resucitaría una
      // conversación que el usuario dio por cerrada.
      if (aborto.signal.aborted) return;
      hilo.current = respuesta.hilo;

      const tardo = performance.now() - arranco;

      if (respuesta.metricas.llamadasAlModelo > 0) {
        // De acá sale la media con la que se retiene a los otros.
        medidor.current.anotar(tardo);
      } else if (respuesta.estado !== "servicio_degradado") {
        // ESPERA PAREJA. Un carril determinista contesta en milisegundos, y esa
        // respuesta instantánea se lee como «no hizo nada». Se retiene hasta
        // parecerse a un turno con modelo, con lo que ya tardó descontado.
        //
        // El degradado queda AFUERA a propósito: es el sistema avisando que no
        // está disponible, y hacer esperar a alguien para darle esa noticia es la
        // clase de coherencia que no vale lo que cuesta.
        await esperarHasta(medidor.current.objetivoMs() - tardo, aborto.signal);
        // La espera es abortable: si se dejó de esperar mientras corría, esta
        // respuesta ya no es de nadie.
        if (aborto.signal.aborted) return;
      }

      if (!montado.current) return;
      setTurnos((previos) => previos.map((t) => (t.id === id ? { ...t, respuesta } : t)));
    } catch (error) {
      // Un aborto no es un error: lo pidió este lado. El turno queda como está.
      if (esCancelacion(error)) return;
      // Un hilo que el backend ya no reconoce no se vuelve a mandar: la siguiente
      // pregunta abre una conversación nueva en lugar de repetir el mismo 404.
      if (esHiloPerdido(error)) hilo.current = null;
      if (!montado.current) return;
      setTurnos((previos) =>
        previos.map((t) => (t.id === id ? { ...t, error: mensajeDeError(error) } : t)),
      );
    } finally {
      // Sólo si este turno sigue siendo el actual: uno que se dejó de esperar
      // termina de rechazarse cuando quizá ya hay otro en vuelo, y ése no es suyo.
      if (enCurso.current?.aborto === aborto) {
        enCurso.current = null;
        if (montado.current) setEnVuelo(false);
      }
    }
  }, []);

  const preguntar = useCallback(
    async (mensaje: string) => {
      const texto = mensaje.trim();
      if (texto.length === 0) return;

      // UN TURNO A LA VEZ. Dos pedidos concurrentes son dos claves de idempotencia
      // —dos cobros— y el segundo sale con el hilo viejo o nulo: abre otra
      // conversación que nadie pidió. El guard vive acá y no sólo en la vista para
      // que ningún montaje futuro lo pierda.
      if (enCurso.current) return;

      // Una clave POR INTENTO, que es también el id del turno. Reusarla entre
      // turnos haría que el segundo recibiera la respuesta del primero, que es
      // justo lo contrario de lo que se busca.
      const id = crypto.randomUUID();
      setTurnos((previos) => [...previos, { id, pregunta: texto }]);
      await enviar(id, texto);
    },
    [enviar],
  );

  const reintentar = useCallback(
    async (id: string) => {
      if (enCurso.current) return;
      const turno = turnos.find((t) => t.id === id);
      // SÓLO UN TURNO QUE TERMINÓ EN ERROR. La idempotencia del backend consulta la
      // caché antes de ejecutar y guarda después, sin registrar el turno en curso:
      // la misma clave mientras el original sigue corriendo ejecutaría el turno
      // entero otra vez. Uno que se dejó de esperar sigue corriendo allá.
      if (!turno?.error) return;

      // Misma clave —el id—, mismo texto y mismo objetivo de reemplazo si
      // había uno: si el backend ya había terminado cuando se cortó,
      // devuelve lo que guardó en lugar de cobrarle otra vez al modelo, y
      // aplica el reemplazo a lo sumo una vez (asistente-edicion-de-la-
      // ultima-pregunta).
      setTurnos((previos) =>
        previos.map((t) =>
          t.id === id ? { id: t.id, pregunta: t.pregunta, reemplaza: t.reemplaza } : t,
        ),
      );
      await enviar(id, turno.pregunta, turno.reemplaza);
    },
    [turnos, enviar],
  );

  // «EDITAR Y REENVIAR». Sólo tiene sentido sobre la ÚLTIMA pregunta —el
  // backend lo exige igual (design.md D9: 409 si no lo es)—, así que se
  // toma sin pedirle el id a quien llama: es siempre `turnos[^1]`.
  const reenviarUltima = useCallback(
    async (texto: string) => {
      if (enCurso.current) return;
      const limpio = texto.trim();
      if (limpio.length === 0) return;

      const ultimo = turnos[turnos.length - 1];
      if (!ultimo) return;

      // Un `id` NUEVO, nunca el del turno reemplazado: es la clave de
      // idempotencia de ESTE envío, y es también lo que hace que la lista le
      // dé una `key` distinta al turno — así el voto, el orden de la tabla y
      // la vista ampliada del turno reemplazado se resetean solos al volver
      // a montarse, sin código propio que los limpie a mano.
      const id = crypto.randomUUID();
      const reemplaza = ultimo.id;

      setTurnos((previos) => [...previos.slice(0, -1), { id, pregunta: limpio, reemplaza }]);
      await enviar(id, limpio, reemplaza);
    },
    [turnos, enviar],
  );

  const detener = useCallback(() => {
    const actual = enCurso.current;
    if (!actual) return;

    // Se suelta el request y se libera el campo ya, sin esperar a que la promesa
    // termine de rechazarse. El backend no se entera: sigue el turno hasta el
    // final y lo cobra, y eso es lo que el turno le dice al usuario.
    enCurso.current = null;
    actual.aborto.abort();
    setEnVuelo(false);
    setTurnos((previos) => previos.map((t) => (t.id === actual.id ? { ...t, detenido: true } : t)));
  }, []);

  const reiniciar = useCallback(() => {
    // Lo que estuviera en vuelo se suelta con la conversación que lo pidió.
    enCurso.current?.aborto.abort();
    enCurso.current = null;
    hilo.current = null;
    setEnVuelo(false);
    setTurnos([]);
  }, []);

  // REANUDAR REEMPLAZA LA CONVERSACIÓN EN CURSO. El backend ya mintió un hilo
  // efímero nuevo (design.md D3): lo que corresponde de este lado es lo mismo
  // que «Nueva conversación» hace con lo que había —soltar lo que estuviera en
  // vuelo— pero pintando los turnos restaurados en vez de vaciar la lista.
  const sembrarDesdeHistorial = useCallback(
    (hiloEfimero: string, turnosHistoricos: TurnoDeHistorial[]) => {
      enCurso.current?.aborto.abort();
      enCurso.current = null;
      hilo.current = hiloEfimero;
      setEnVuelo(false);
      setTurnos(
        turnosHistoricos.map((t) => ({
          id: t.id,
          pregunta: t.pregunta,
          historico: { estado: t.estado, sql: t.sql ?? null, ocurrioEn: t.ocurrioEn },
        })),
      );
    },
    [],
  );

  // «VOLVER A CONSULTAR». Sólo aplica a un turno histórico —el backend ya lo
  // exige (`Respondida` con `sql_resuelto`), y del lado del cliente el botón
  // que la dispara sólo existe sobre uno—, así que un turno que no lo sea, o
  // que ya no esté en la lista, no hace nada: no hay ningún caso al que
  // llegue sin ese estado.
  const reejecutar = useCallback(async (id: string) => {
    setTurnos((previos) =>
      previos.map((t) =>
        t.id === id && t.historico
          ? { ...t, historico: { ...t.historico, reejecutando: true } }
          : t,
      ),
    );

    try {
      const resultado = await reejecutarTurno(id);
      if (!montado.current) return;
      setTurnos((previos) =>
        previos.map((t) =>
          t.id === id && t.historico
            ? { ...t, historico: { ...t.historico, reejecutando: false, reejecucion: resultado } }
            : t,
        ),
      );
    } catch {
      // Un fallo de TRANSPORTE, no el rechazo prolijo que ya modela `exitosa:
      // false` —ése llega en un 200 y no entra a este catch—. Mismo criterio
      // que el resto del hook: nunca nada crudo llega al usuario.
      if (!montado.current) return;
      setTurnos((previos) =>
        previos.map((t) =>
          t.id === id && t.historico
            ? {
                ...t,
                historico: {
                  ...t.historico,
                  reejecutando: false,
                  reejecucion: {
                    exitosa: false,
                    mensaje:
                      "No pude volver a ejecutar esa consulta. Probá de nuevo en un momento.",
                    columnas: [],
                    filas: [],
                    truncado: false,
                  },
                },
              }
            : t,
        ),
      );
    }
  }, []);

  return {
    turnos,
    enVuelo,
    preguntar,
    reintentar,
    reenviarUltima,
    reiniciar,
    detener,
    sembrarDesdeHistorial,
    reejecutar,
  };
}
