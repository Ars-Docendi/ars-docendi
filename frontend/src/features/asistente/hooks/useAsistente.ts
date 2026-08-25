import { useCallback, useRef, useState } from "react";

import { consultar } from "../api/asistenteApi";
import { mensajeDeError } from "../errores";
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

  const preguntar = useCallback(async (mensaje: string) => {
    const texto = mensaje.trim();
    if (texto.length === 0) return;

    // Una clave POR INTENTO. Reusarla entre turnos haría que el segundo recibiera
    // la respuesta del primero, que es justo lo contrario de lo que se busca.
    const clave = crypto.randomUUID();
    const id = clave;

    setTurnos((previos) => [...previos, { id, pregunta: texto }]);
    setEnVuelo(true);

    try {
      const respuesta = await consultar({ mensaje: texto, hilo: hilo.current }, clave);
      hilo.current = respuesta.hilo;
      setTurnos((previos) => previos.map((t) => (t.id === id ? { ...t, respuesta } : t)));
    } catch (error) {
      setTurnos((previos) =>
        previos.map((t) => (t.id === id ? { ...t, error: mensajeDeError(error) } : t)),
      );
    } finally {
      setEnVuelo(false);
    }
  }, []);

  return { turnos, enVuelo, preguntar };
}
