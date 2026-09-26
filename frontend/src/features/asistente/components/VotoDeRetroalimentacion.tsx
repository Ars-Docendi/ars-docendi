import { useState } from "react";
import { Button } from "@ars-docendi/ui";

import { thumbDownIcon, thumbUpIcon } from "../../../app/shell/icons";
import { enviarRetroalimentacion } from "../api/asistenteApi";
import type { RazonDeRetroalimentacion } from "../types";

interface VotoDeRetroalimentacionProps {
  /** Absent on any turn that is not an answered one: nothing renders then. */
  claveDeRetroalimentacion?: string | null;
}

const RAZONES: ReadonlyArray<{ valor: RazonDeRetroalimentacion; etiqueta: string }> = [
  { valor: "datos_incorrectos", etiqueta: "Datos incorrectos" },
  { valor: "no_entendio_la_pregunta", etiqueta: "No entendió la pregunta" },
  { valor: "lento", etiqueta: "Es lento" },
  { valor: "otro", etiqueta: "Otro motivo" },
];

/**
 * Thumbs up/down for an answered turn.
 *
 * Renders nothing without a token — there is nothing to rate without one, and
 * the backend would 404 anyway. A thumbs-up submits right away; a thumbs-down
 * opens the four reason choices first and submits only from there, including
 * with no reason picked.
 *
 * State lives locally and not in `TurnoDeLaConversacion`: this component stays
 * mounted for the life of its turn (keyed by `turno.id` in `Conversacion`), so
 * plain `useState` already gives "the recorded vote shows as active when you
 * look again" for free, with no extra plumbing.
 *
 * The confirmation text below the buttons is picked up by the SAME live
 * region `Conversacion.tsx` already declares (`role="log" aria-live="polite"`)
 * — there is no separate one here — and nothing here ever moves focus.
 */
export function VotoDeRetroalimentacion({
  claveDeRetroalimentacion,
}: VotoDeRetroalimentacionProps) {
  const [voto, setVoto] = useState<boolean | null>(null);
  const [mostrarRazones, setMostrarRazones] = useState(false);
  const [confirmacion, setConfirmacion] = useState<string | null>(null);

  if (!claveDeRetroalimentacion) return null;

  async function enviar(nuevoVoto: boolean, razon?: RazonDeRetroalimentacion) {
    try {
      await enviarRetroalimentacion({
        token: claveDeRetroalimentacion!,
        voto: nuevoVoto,
        razon,
      });
    } catch {
      // El token venció, o la red falló. No hay nada más que ofrecer acá: los
      // botones siguen disponibles y el usuario puede reintentar.
      return;
    }

    setVoto(nuevoVoto);
    setMostrarRazones(false);
    setConfirmacion(
      nuevoVoto ? "Se registró tu voto: te sirvió." : "Se registró tu voto: no te sirvió.",
    );
  }

  return (
    <div className="adoc-asistente-voto">
      <div
        className="adoc-asistente-voto-botones"
        role="group"
        aria-label="Calificar esta respuesta"
      >
        <Button
          variant="ghost"
          size="sm"
          leadingIcon={thumbUpIcon}
          aria-pressed={voto === true}
          onClick={() => void enviar(true)}
        >
          Me sirvió
        </Button>
        <Button
          variant="ghost"
          size="sm"
          leadingIcon={thumbDownIcon}
          aria-pressed={voto === false}
          onClick={() => setMostrarRazones(true)}
        >
          No me sirvió
        </Button>
      </div>

      {mostrarRazones && (
        <div className="adoc-asistente-voto-razones">
          <p className="adoc-asistente-voto-razones-titulo">¿Por qué no te sirvió? (opcional)</p>
          <ul className="adoc-asistente-chips">
            {RAZONES.map((razon) => (
              <li key={razon.valor}>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => void enviar(false, razon.valor)}
                >
                  {razon.etiqueta}
                </Button>
              </li>
            ))}
          </ul>
          <Button variant="ghost" size="sm" onClick={() => void enviar(false)}>
            Enviar sin especificar motivo
          </Button>
        </div>
      )}

      {confirmacion && <p className="adoc-asistente-voto-confirmacion">{confirmacion}</p>}
    </div>
  );
}
