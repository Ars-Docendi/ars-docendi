import { useEffect, useRef, useState } from "react";

import { Conversacion } from "./Conversacion";
import { EntradaDePregunta } from "./EntradaDePregunta";
import { FranjaDeEstado } from "./FranjaDeEstado";
import { Sugerencias } from "./Sugerencias";
import { useAccesoAlAsistente } from "../hooks/useAccesoAlAsistente";
import { useAsistente } from "../hooks/useAsistente";

interface PanelAsistenteProps {
  /** Para el test del umbral, que no puede esperar el tiempo real. */
  umbralDelIndicadorMs?: number;
}

/**
 * La vista del asistente.
 *
 * ES UNA SOLA, MONTADA DOS VECES: la ruta `/asistente` la muestra a página completa
 * y el lanzador de la barra superior la muestra en un modal. Dos implementaciones se
 * desincronizarían —una recibiría una mejora y la otra no—, y nadie lo notaría hasta
 * que alguien reportara que «desde el botón anda distinto».
 */
export function PanelAsistente({ umbralDelIndicadorMs }: PanelAsistenteProps) {
  const { capacidades } = useAccesoAlAsistente();
  const { turnos, enVuelo, preguntar } = useAsistente();
  const [borrador, setBorrador] = useState("");
  const entrada = useRef<HTMLTextAreaElement>(null);
  const hilo = useRef<HTMLDivElement>(null);

  // El foco vuelve al campo cuando el turno termina: quien está usando un lector de
  // pantalla o el teclado no tiene que volver a buscarlo para seguir preguntando.
  useEffect(() => {
    if (!enVuelo) entrada.current?.focus();
  }, [enVuelo]);

  // El hilo sigue a lo último. Con el campo de entrada fijo abajo, una respuesta que
  // aparece bajo el pliegue es una respuesta que no se ve: quien preguntó se queda
  // mirando la pregunta anterior sin saber que ya le contestaron.
  //
  // `auto` y no `smooth` a propósito: la animación tarda, y durante ese rato el
  // texto se mueve debajo de quien está tratando de leerlo.
  useEffect(() => {
    const contenedor = hilo.current;
    if (contenedor) contenedor.scrollTop = contenedor.scrollHeight;
  }, [turnos, enVuelo]);

  async function enviar(mensaje: string) {
    // Mientras hay un turno en vuelo no se envía nada —ni por Enter, ni por el
    // botón, ni por un chip—, pero se puede seguir escribiendo: el borrador no se
    // toca. El hook tiene su propio guard; éste es el que cuida lo escrito.
    if (enVuelo) return;
    setBorrador("");
    await preguntar(mensaje);
  }

  return (
    <section className="adoc-asistente" aria-label="Asistente conversacional">
      {/* LO QUE SCROLLEA ES ESTO, y no el modal entero. Con el modal scrolleando, el
          campo de entrada se va hacia abajo con cada respuesta y hay que perseguirlo;
          acá se queda quieto y lo que se mueve es la conversación, que es lo que uno
          espera de un chat. */}
      <div className="adoc-asistente-hilo" ref={hilo}>
        {turnos.length === 0 && capacidades && (
          <div className="adoc-asistente-inicio">
            <p>
              Puedo consultar {capacidades.tablas} áreas de datos del sistema. {capacidades.alcance}
            </p>
            <Sugerencias
              sugerencias={capacidades.ejemplos}
              onElegir={enviar}
              deshabilitado={enVuelo}
            />
            <ul className="adoc-asistente-limites">
              {capacidades.noPuede.map((limite) => (
                <li key={limite}>{limite}</li>
              ))}
            </ul>
          </div>
        )}

        <Conversacion turnos={turnos} onElegir={enviar} enVuelo={enVuelo} />
      </div>

      {/* Una sola fila, FUERA de la región viva a propósito. */}
      <FranjaDeEstado enVuelo={enVuelo} turnos={turnos} umbralMs={umbralDelIndicadorMs} />

      <EntradaDePregunta
        ref={entrada}
        valor={borrador}
        onCambiar={setBorrador}
        onEnviar={() => void enviar(borrador)}
        enVuelo={enVuelo}
      />
    </section>
  );
}
