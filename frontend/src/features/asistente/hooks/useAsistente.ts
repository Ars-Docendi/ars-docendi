import { useCallback, useRef, useState } from "react";

import { consultar } from "../api/asistenteApi";
import { esHiloPerdido, mensajeDeError } from "../errores";
import type { TurnoDeLaConversacion } from "../types";

export interface Asistente {
  turnos: TurnoDeLaConversacion[];
  enVuelo: boolean;
  preguntar: (mensaje: string) => Promise<void>;
}

/**
 * La conversación de esta sesión.
 *
 * El hilo vive acá y no en un store global. El backend ya decidió no persistirlo, y
 * un store agregaría decisiones de ciclo de vida —cuándo se limpia, qué pasa al
 * cambiar de rol— para un estado que muere igual al recargar la página.
 */
export function useAsistente(): Asistente {
  const [turnos, setTurnos] = useState<TurnoDeLaConversacion[]>([]);
  const [enVuelo, setEnVuelo] = useState(false);
  const hilo = useRef<string | null>(null);
  // Espejo de `enVuelo` para el guard de abajo. `preguntar` se memoiza una sola
  // vez, así que leer el estado adentro daría siempre el valor del primer render.
  const turnoEnVuelo = useRef(false);

  const preguntar = useCallback(async (mensaje: string) => {
    const texto = mensaje.trim();
    if (texto.length === 0) return;

    // UN TURNO A LA VEZ. Dos pedidos concurrentes son dos claves de idempotencia
    // —dos cobros— y el segundo sale con el hilo viejo o nulo: abre otra
    // conversación que nadie pidió. El guard vive acá y no sólo en la vista para
    // que ningún montaje futuro lo pierda.
    if (turnoEnVuelo.current) return;

    // Una clave POR INTENTO. Reusarla entre turnos haría que el segundo recibiera
    // la respuesta del primero, que es justo lo contrario de lo que se busca.
    const clave = crypto.randomUUID();
    const id = clave;

    setTurnos((previos) => [...previos, { id, pregunta: texto }]);
    turnoEnVuelo.current = true;
    setEnVuelo(true);

    try {
      const respuesta = await consultar({ mensaje: texto, hilo: hilo.current }, clave);
      hilo.current = respuesta.hilo;
      setTurnos((previos) => previos.map((t) => (t.id === id ? { ...t, respuesta } : t)));
    } catch (error) {
      // Un hilo que el backend ya no reconoce no se vuelve a mandar: la siguiente
      // pregunta abre una conversación nueva en lugar de repetir el mismo 404.
      if (esHiloPerdido(error)) hilo.current = null;
      setTurnos((previos) =>
        previos.map((t) => (t.id === id ? { ...t, error: mensajeDeError(error) } : t)),
      );
    } finally {
      turnoEnVuelo.current = false;
      setEnVuelo(false);
    }
  }, []);

  return { turnos, enVuelo, preguntar };
}
