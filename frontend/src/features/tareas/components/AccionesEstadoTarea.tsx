import { useState } from "react";
import { Button, Field, InlineAlert, Input, Textarea } from "@ars-docendi/ui";
import { puedeCambiarEstado, puedeEditarAvance } from "../api/maquinaEstadosTarea";
import type { ActorTarea, EstadoTarea, Tarea } from "../types";

interface AccionesEstadoTareaProps {
  tarea: Tarea;
  actor: ActorTarea;
  enviando: boolean;
  error?: string;
  onCambiarEstado: (
    estadoDestino: EstadoTarea,
    opciones?: { comentario?: string; solucion?: string },
  ) => void;
  onEditarAvance: (porcentajeAvance: number) => void;
}

const ETIQUETA_ESTADO: Record<EstadoTarea, string> = {
  pendiente: "Pendiente",
  en_curso: "En curso",
  pausa: "Pausa",
  resuelta: "Resuelta",
  cancelada: "Cancelada",
};

const ESTADOS_DISPONIBLES: EstadoTarea[] = ["pendiente", "en_curso", "pausa", "resuelta"];

/**
 * Panel de acciones del rail lateral del detalle: cambio de estado (Pausa
 * exige comentario, Resuelta exige Solución) y edición del % de avance.
 * La visibilidad de cada control se deriva de `maquinaEstadosTarea.ts` —
 * Cancelar y la edición de campos viven en el header de la página
 * (mismo split que `PanelAccionesRevision`/header en Designaciones).
 */
export function AccionesEstadoTarea({
  tarea,
  actor,
  enviando,
  error,
  onCambiarEstado,
  onEditarAvance,
}: AccionesEstadoTareaProps) {
  const [destinoPendiente, setDestinoPendiente] = useState<EstadoTarea | null>(null);
  const [texto, setTexto] = useState("");
  const [avance, setAvance] = useState(String(tarea.porcentajeAvance));

  const estadosPosibles = ESTADOS_DISPONIBLES.filter(
    (destino) => destino !== tarea.estado && puedeCambiarEstado(tarea, actor, destino),
  );
  const puedeAvance = puedeEditarAvance(tarea, actor);

  function iniciarCambio(destino: EstadoTarea) {
    setTexto("");
    if (destino === "pausa" || destino === "resuelta") {
      setDestinoPendiente(destino);
      return;
    }
    onCambiarEstado(destino);
  }

  function confirmarConTexto() {
    if (!destinoPendiente || !texto.trim()) return;
    if (destinoPendiente === "pausa") {
      onCambiarEstado("pausa", { comentario: texto.trim() });
    } else {
      onCambiarEstado("resuelta", { solucion: texto.trim() });
    }
    setDestinoPendiente(null);
    setTexto("");
  }

  if (estadosPosibles.length === 0 && !puedeAvance) {
    return null;
  }

  return (
    <section className="adoc-det-tarea-panel" aria-label="Acciones sobre la tarea">
      <h2>Acciones</h2>

      {error && (
        <div style={{ marginBottom: "10px" }}>
          <InlineAlert severity="danger" title="No se pudo aplicar la acción">
            {error}
          </InlineAlert>
        </div>
      )}

      {estadosPosibles.length > 0 && (
        <div style={{ display: "flex", flexDirection: "column", gap: "8px", marginBottom: "16px" }}>
          <span style={{ fontSize: "12px", color: "var(--color-text-tertiary)" }}>
            Cambiar estado
          </span>
          <div style={{ display: "flex", flexWrap: "wrap", gap: "8px" }}>
            {estadosPosibles.map((destino) => (
              <Button
                key={destino}
                variant="secondary"
                size="sm"
                disabled={enviando}
                onClick={() => iniciarCambio(destino)}
              >
                {ETIQUETA_ESTADO[destino]}
              </Button>
            ))}
          </div>

          {destinoPendiente && (
            <div style={{ display: "flex", flexDirection: "column", gap: "8px", marginTop: "4px" }}>
              <Field
                label={destinoPendiente === "pausa" ? "Motivo de la consulta" : "Solución"}
                required
                error={!texto.trim() ? "Campo obligatorio para confirmar" : undefined}
              >
                <Textarea
                  value={texto}
                  onChange={(e) => setTexto(e.target.value)}
                  rows={2}
                  placeholder={
                    destinoPendiente === "pausa"
                      ? "Contá qué necesitás consultar antes de seguir…"
                      : "Contá cómo se resolvió la tarea…"
                  }
                />
              </Field>
              <div style={{ display: "flex", gap: "8px" }}>
                <Button variant="secondary" size="sm" onClick={() => setDestinoPendiente(null)}>
                  Cancelar
                </Button>
                <Button
                  variant="primary"
                  size="sm"
                  disabled={!texto.trim() || enviando}
                  loading={enviando}
                  onClick={confirmarConTexto}
                >
                  Confirmar {ETIQUETA_ESTADO[destinoPendiente]}
                </Button>
              </div>
            </div>
          )}
        </div>
      )}

      {puedeAvance && (
        <Field label="% de avance">
          <div style={{ display: "flex", gap: "8px" }}>
            <Input
              type="number"
              min={0}
              max={100}
              value={avance}
              onChange={(e) => setAvance(e.target.value)}
              aria-label="Porcentaje de avance"
            />
            <Button
              variant="secondary"
              size="sm"
              disabled={enviando || avance === String(tarea.porcentajeAvance)}
              onClick={() => onEditarAvance(Number(avance))}
            >
              Guardar
            </Button>
          </div>
        </Field>
      )}
    </section>
  );
}
