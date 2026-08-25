import { useEffect, useRef, useState } from "react";
import { Button, Textarea } from "@ars-docendi/ui";

import { Conversacion } from "./Conversacion";
import { IndicadorDeProceso } from "./IndicadorDeProceso";
import { LineaDeMetricas } from "./LineaDeMetricas";
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
 * y el lanzador de la barra superior la muestra en un cajón. Dos implementaciones se
 * desincronizarían —una recibiría una mejora y la otra no—, y nadie lo notaría hasta
 * que alguien reportara que «desde el botón anda distinto».
 */
export function PanelAsistente({ umbralDelIndicadorMs }: PanelAsistenteProps) {
  const { capacidades } = useAccesoAlAsistente();
  const { turnos, enVuelo, preguntar } = useAsistente();
  const [borrador, setBorrador] = useState("");
  const entrada = useRef<HTMLTextAreaElement>(null);

  // El foco vuelve al campo cuando el turno termina: quien está usando un lector de
  // pantalla o el teclado no tiene que volver a buscarlo para seguir preguntando.
  useEffect(() => {
    if (!enVuelo) entrada.current?.focus();
  }, [enVuelo]);

  async function enviar(mensaje: string) {
    setBorrador("");
    await preguntar(mensaje);
  }

  return (
    <section className="adoc-asistente" aria-label="Asistente conversacional">
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

      {/* Los dos van FUERA de la región viva, a propósito. */}
      <IndicadorDeProceso activo={enVuelo} umbralMs={umbralDelIndicadorMs} />
      <LineaDeMetricas turnos={turnos} />

      <form
        className="adoc-asistente-entrada"
        onSubmit={(evento) => {
          evento.preventDefault();
          void enviar(borrador);
        }}
      >
        <Textarea
          ref={entrada}
          rows={2}
          value={borrador}
          onChange={(evento) => setBorrador(evento.target.value)}
          placeholder="Preguntá algo sobre designaciones, docentes, materias o pedidos"
          aria-label="Tu pregunta"
          onKeyDown={(evento) => {
            // Enter envía, Shift+Enter hace salto de línea: es lo que espera
            // cualquiera que haya usado un chat.
            if (evento.key === "Enter" && !evento.shiftKey) {
              evento.preventDefault();
              void enviar(borrador);
            }
          }}
        />
        <Button type="submit" disabled={enVuelo || borrador.trim().length === 0}>
          Preguntar
        </Button>
      </form>
    </section>
  );
}
