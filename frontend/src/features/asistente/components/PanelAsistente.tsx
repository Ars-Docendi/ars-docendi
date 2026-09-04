import { useEffect, useRef, useState } from "react";
import { InlineAlert } from "@ars-docendi/ui";

import { Conversacion } from "./Conversacion";
import { EntradaDePregunta } from "./EntradaDePregunta";
import { EstadoInicial } from "./EstadoInicial";
import { FranjaDeEstado } from "./FranjaDeEstado";
import { IrAlFinal } from "./IrAlFinal";
import { NuevaConversacion } from "./NuevaConversacion";
import { MENSAJE_SIN_ACCESO } from "../errores";
import { useAccesoAlAsistente } from "../hooks/useAccesoAlAsistente";
import { useAnclaAlFinal } from "../hooks/useAnclaAlFinal";
import type { Asistente } from "../hooks/useAsistente";

interface PanelAsistenteProps {
  /**
   * La conversación. La crea el dueño del montaje —el lanzador para el modal, la
   * página para la ruta— y no el panel, para que sobreviva a cerrar el modal.
   */
  asistente: Asistente;
  /**
   * Con un encabezado propio que lleva «Nueva conversación». Lo pide el modal; en
   * la ruta el botón va en el encabezado de la página.
   */
  mostrarEncabezado?: boolean;
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
 *
 * NO TIENE CONVERSACIÓN PROPIA: la recibe. En el modal se monta al abrir y se
 * desmonta al cerrar, y si el hilo viviera acá se iría con él —Esc y un clic
 * afuera, también sin querer, cierran—. Lo que sí es suyo es lo que se está
 * escribiendo y el foco.
 */
export function PanelAsistente({
  asistente,
  mostrarEncabezado = false,
  umbralDelIndicadorMs,
}: PanelAsistenteProps) {
  const { capacidades, tieneAcceso } = useAccesoAlAsistente();
  const { turnos, enVuelo, preguntar, reintentar, detener } = asistente;
  const [borrador, setBorrador] = useState("");
  const entrada = useRef<HTMLTextAreaElement>(null);
  const sinTurnos = turnos.length === 0;

  // El foco vuelve al campo cuando el turno termina —también cuando se lo dejó de
  // esperar— y cuando la conversación se vacía: quien está usando un lector de
  // pantalla o el teclado no tiene que volver a buscarlo para seguir preguntando.
  // Se mira el estado y no quién lo cambió, porque «Nueva conversación» puede
  // vivir fuera del panel —en la ruta va en el encabezado de la página— y el
  // campo es de acá.
  useEffect(() => {
    if (!enVuelo) entrada.current?.focus();
  }, [enVuelo, sinTurnos]);

  // El hilo sigue a quien está abajo y no arrastra a quien subió: al enviar va al
  // fondo, la respuesta se muestra desde su inicio, y si el usuario subió a releer
  // se queda donde está con «Ir al final» a mano.
  const { hilo, anclado, irAlFinal, onScroll } = useAnclaAlFinal(turnos);

  async function enviar(mensaje: string) {
    // Mientras hay un turno en vuelo no se envía nada —ni por Enter, ni por el
    // botón, ni por un chip—, pero se puede seguir escribiendo: el borrador no se
    // toca. El hook tiene su propio guard; éste es el que cuida lo escrito.
    if (enVuelo) return;
    setBorrador("");
    await preguntar(mensaje);
  }

  // SIN ACCESO NO HAY FORMULARIO. Con 403 el campo y el botón quedaban activos y
  // el rechazo recién aparecía al enviar: un formulario que aparenta funcionar es
  // el fake UI que el invariante #7 prohíbe. El lanzador de la barra no aparece
  // sin acceso, así que la ruta `/asistente` es la única forma de llegar acá.
  if (tieneAcceso === false) {
    return (
      <section className="adoc-asistente" aria-label="Asistente conversacional">
        <InlineAlert severity="info">{MENSAJE_SIN_ACCESO}</InlineAlert>
      </section>
    );
  }

  return (
    <section className="adoc-asistente" aria-label="Asistente conversacional">
      {mostrarEncabezado && (
        <div className="adoc-asistente-encabezado">
          <NuevaConversacion asistente={asistente} />
        </div>
      )}

      {/* LO QUE SCROLLEA ES ESTO, y no el modal entero. Con el modal scrolleando, el
          campo de entrada se va hacia abajo con cada respuesta y hay que perseguirlo;
          acá se queda quieto y lo que se mueve es la conversación, que es lo que uno
          espera de un chat. */}
      <div className="adoc-asistente-hilo-marco">
        <div className="adoc-asistente-hilo" ref={hilo} onScroll={onScroll}>
          {sinTurnos && capacidades && (
            <EstadoInicial capacidades={capacidades} onElegir={enviar} deshabilitado={enVuelo} />
          )}

          <Conversacion
            turnos={turnos}
            onElegir={enviar}
            onReintentar={(id) => void reintentar(id)}
            enVuelo={enVuelo}
          />
        </div>

        {/* Flota sobre el hilo, fuera de la región viva. Al pulsarlo desaparece, y
            el foco que tenía se iría a ninguna parte: pasa al campo, que es lo que
            hay en el final al que se acaba de ir. */}
        <IrAlFinal
          visible={!anclado}
          onClick={() => {
            irAlFinal();
            entrada.current?.focus();
          }}
        />
      </div>

      {/* Una sola fila, FUERA de la región viva a propósito. */}
      <FranjaDeEstado
        enVuelo={enVuelo}
        turnos={turnos}
        onDetener={detener}
        umbralMs={umbralDelIndicadorMs}
      />

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
