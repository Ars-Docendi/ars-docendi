import { useId, useState } from "react";
import { Button } from "@ars-docendi/ui";

import { checkIcon, thumbDownIcon, thumbUpIcon } from "../../../app/shell/icons";
import { enviarRetroalimentacion } from "../api/asistenteApi";
import type { RazonDeRetroalimentacion } from "../types";

interface VotoDeRetroalimentacionProps {
  /** Absent on any turn that is not an answered one: nothing renders then. */
  claveDeRetroalimentacion?: string | null;
}

const RAZONES: ReadonlyArray<{ valor: RazonDeRetroalimentacion; etiqueta: string }> = [
  { valor: "datos_incorrectos", etiqueta: "Datos incorrectos" },
  { valor: "no_entendio_la_pregunta", etiqueta: "No entendió la pregunta" },
  { valor: "faltan_datos", etiqueta: "Faltan datos" },
  { valor: "otro", etiqueta: "Otro" },
];

/** Espejo de `LargoMaximoDelComentario` en `AsistenteController` (backend). */
const LARGO_MAXIMO_DEL_COMENTARIO = 500;

/**
 * Thumbs up/down for an answered turn, en v3 (asistente-rediseno-v3, design.md
 * D6/D7/D14, PO-changed 2026-09-26): dos íconos —no texto— dentro de la barra de
 * acciones (`BarraDeAcciones`), y el panel «¿Qué falló?» con pastillas de
 * elección múltiple más un comentario libre acotado, igual que el mock.
 *
 * Renders nothing without a token — there is nothing to rate without one, and
 * the backend would 404 anyway. A thumbs-up submits right away; a thumbs-down
 * opens the reason panel first and submits only from there («Omitir» o
 * «Enviar comentario»), incluso sin ninguna pastilla elegida ni comentario.
 *
 * State lives locally and not in `TurnoDeLaConversacion`: this component stays
 * mounted for the life of its turn (keyed by `turno.id` in `Conversacion`), so
 * plain `useState` already gives "the recorded vote shows as active when you
 * look again" for free, with no extra plumbing.
 *
 * SIN `display` PROPIO: `.adoc-asistente-voto` es `display: contents` en
 * `asistente.css`, así que sus hijos —los dos botones y, si corresponde, el
 * panel/agradecimiento— pasan a ser ítems directos de la fila de
 * `BarraDeAcciones` sin que este componente sepa nada de layout ajeno.
 *
 * The confirmation text is picked up by the SAME live region `Conversacion.tsx`
 * already declares (`role="log" aria-live="polite"`) — there is no separate one
 * here — and nothing here ever moves focus.
 *
 * El comentario nunca se loguea, nunca viaja al modelo y no tiene superficie de
 * lectura en ningún lugar de la interfaz (asistente-retroalimentacion's spec;
 * adenda de TD-012 en docs/quality/tech-debt.md).
 */
export function VotoDeRetroalimentacion({
  claveDeRetroalimentacion,
}: VotoDeRetroalimentacionProps) {
  const idComentario = useId();
  const [voto, setVoto] = useState<boolean | null>(null);
  const [mostrarPanel, setMostrarPanel] = useState(false);
  const [razonesElegidas, setRazonesElegidas] = useState<RazonDeRetroalimentacion[]>([]);
  const [comentario, setComentario] = useState("");
  const [confirmacion, setConfirmacion] = useState<string | null>(null);

  if (!claveDeRetroalimentacion) return null;

  async function enviar(
    nuevoVoto: boolean,
    razones?: RazonDeRetroalimentacion[],
    comentarioAEnviar?: string,
  ) {
    const comentarioRecortado = comentarioAEnviar?.trim();

    try {
      await enviarRetroalimentacion({
        token: claveDeRetroalimentacion!,
        voto: nuevoVoto,
        razones: razones?.length ? razones : undefined,
        comentario: comentarioRecortado ? comentarioRecortado : undefined,
      });
    } catch {
      // El token venció, o la red falló. No hay nada más que ofrecer acá: los
      // botones siguen disponibles y el usuario puede reintentar.
      return;
    }

    setVoto(nuevoVoto);
    setMostrarPanel(false);
    setRazonesElegidas([]);
    setComentario("");
    // El agradecimiento es sólo del flujo de «No sirvió» (design.md D14):
    // un «Sirvió» no abre panel, así que su propia confirmación alcanza.
    setConfirmacion(
      nuevoVoto
        ? "Se registró tu voto: te sirvió."
        : "Gracias. Tu comentario ayuda a mejorar el asistente.",
    );
  }

  function alPulsarNoSirvio() {
    setMostrarPanel(true);
    setConfirmacion(null);
  }

  function alternarRazon(razon: RazonDeRetroalimentacion) {
    // Elección múltiple: cada pastilla se prende o apaga sola.
    setRazonesElegidas((actuales) =>
      actuales.includes(razon) ? actuales.filter((r) => r !== razon) : [...actuales, razon],
    );
  }

  return (
    <div className="adoc-asistente-voto">
      <div
        className="adoc-asistente-voto-botones"
        role="group"
        aria-label="Calificar esta respuesta"
      >
        <button
          type="button"
          className="adoc-asistente-barra-boton adoc-asistente-voto-arriba"
          title="Sirvió"
          aria-label="Sirvió"
          aria-pressed={voto === true}
          onClick={() => void enviar(true)}
        >
          {thumbUpIcon}
        </button>
        <button
          type="button"
          className="adoc-asistente-barra-boton adoc-asistente-voto-abajo"
          title="No sirvió"
          aria-label="No sirvió"
          aria-pressed={voto === false}
          onClick={alPulsarNoSirvio}
        >
          {thumbDownIcon}
        </button>
      </div>

      {mostrarPanel && (
        <div className="adoc-asistente-voto-panel">
          <p className="adoc-asistente-voto-panel-titulo">
            ¿Qué falló? <span className="adoc-asistente-voto-panel-opcional">Opcional</span>
          </p>

          <ul className="adoc-asistente-pastillas">
            {RAZONES.map((razon) => {
              const elegida = razonesElegidas.includes(razon.valor);
              return (
                <li key={razon.valor}>
                  <button
                    type="button"
                    className={
                      elegida ? "adoc-asistente-pastilla elegida" : "adoc-asistente-pastilla"
                    }
                    aria-pressed={elegida}
                    onClick={() => alternarRazon(razon.valor)}
                  >
                    {elegida && (
                      <span className="ico" aria-hidden="true">
                        {checkIcon}
                      </span>
                    )}
                    {razon.etiqueta}
                  </button>
                </li>
              );
            })}
          </ul>

          <div className="adoc-asistente-voto-panel-campo">
            <textarea
              id={idComentario}
              className="adoc-asistente-voto-panel-comentario"
              rows={2}
              maxLength={LARGO_MAXIMO_DEL_COMENTARIO}
              placeholder="Contanos qué esperabas ver…"
              aria-label="Contanos qué esperabas ver"
              value={comentario}
              onChange={(evento) => setComentario(evento.target.value)}
            />
            <div className="adoc-asistente-voto-panel-pie">
              <p className="adoc-asistente-voto-panel-hint">No incluyas datos personales.</p>
              <p className="adoc-asistente-voto-panel-contador" aria-hidden="true">
                {comentario.length}/{LARGO_MAXIMO_DEL_COMENTARIO}
              </p>
            </div>
          </div>

          <div className="adoc-asistente-voto-panel-acciones">
            <Button variant="ghost" size="sm" onClick={() => void enviar(false)}>
              Omitir
            </Button>
            <Button
              variant="primary"
              size="sm"
              onClick={() => void enviar(false, razonesElegidas, comentario)}
            >
              Enviar comentario
            </Button>
          </div>
        </div>
      )}

      {confirmacion && <p className="adoc-asistente-voto-confirmacion">{confirmacion}</p>}
    </div>
  );
}
