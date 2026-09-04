import { useCallback, useEffect, useRef, useState } from "react";

import { consultar } from "../api/asistenteApi";
import { esCancelacion, esHiloPerdido, mensajeDeError } from "../errores";
import type { TurnoDeLaConversacion } from "../types";

export interface Asistente {
  turnos: TurnoDeLaConversacion[];
  enVuelo: boolean;
  preguntar: (mensaje: string) => Promise<void>;
  /** Reenvía un turno que terminó en error, con su misma clave y su mismo texto. */
  reintentar: (id: string) => Promise<void>;
  /** Vacía la conversación y descarta el hilo: la próxima pregunta arranca de cero. */
  reiniciar: () => void;
  /** Deja de esperar el turno en vuelo: suelta el request. El backend lo sigue igual. */
  detener: () => void;
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
 * LO INVOCA EL DUEÑO DEL MONTAJE —el lanzador de la barra para el modal, la página
 * para la ruta— y no el panel. El panel se desmonta al cerrar el modal, y con él se
 * iba la conversación; el lanzador vive con la barra, así que al reabrir el hilo
 * sigue donde estaba. La ruta y el modal son dos hilos distintos.
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

  // Quien se desmonta con un turno en vuelo se lleva el request consigo. Sin esto
  // el pedido sobrevive al componente y la respuesta cae sobre un estado que ya no
  // existe. Corre al desmontarse el DUEÑO del hook: navegar fuera de la ruta
  // aborta; cerrar el modal no, porque el lanzador sigue montado.
  useEffect(() => {
    montado.current = true;
    return () => {
      montado.current = false;
      enCurso.current?.aborto.abort();
    };
  }, []);

  // Un envío, sea el primero o un reintento. Quien llama ya puso el turno en la
  // lista; acá se lo manda y se lo completa con lo que vuelva.
  const enviar = useCallback(async (id: string, texto: string) => {
    const aborto = new AbortController();
    enCurso.current = { id, aborto };
    setEnVuelo(true);

    try {
      const respuesta = await consultar({ mensaje: texto, hilo: hilo.current }, id, {
        signal: aborto.signal,
      });
      // Si mientras tanto se dejó de esperar o se reinició la conversación, lo que
      // llegue ya no es de nadie: tampoco el hilo, que resucitaría una
      // conversación que el usuario dio por cerrada.
      if (aborto.signal.aborted) return;
      hilo.current = respuesta.hilo;
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

      // Misma clave —el id— y mismo texto: si el backend ya había terminado cuando
      // se cortó, devuelve lo que guardó en lugar de cobrarle otra vez al modelo.
      setTurnos((previos) =>
        previos.map((t) => (t.id === id ? { id: t.id, pregunta: t.pregunta } : t)),
      );
      await enviar(id, turno.pregunta);
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

  return { turnos, enVuelo, preguntar, reintentar, reiniciar, detener };
}
