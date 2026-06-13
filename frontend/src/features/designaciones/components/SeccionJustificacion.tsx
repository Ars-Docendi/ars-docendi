import { Field, Textarea } from "@ars-docendi/ui";
import { JUSTIFICACION_MAX, JUSTIFICACION_MIN } from "../mock/mockPedido";

interface SeccionJustificacionProps {
  justificacion: string;
  onCambiar: (valor: string) => void;
}

/** Sección 4 — motivo del pedido, con contador de caracteres (mín. 20 / máx. 1000). */
export function SeccionJustificacion({ justificacion, onCambiar }: SeccionJustificacionProps) {
  const cantidad = justificacion.length;

  const hint = (
    <span style={{ display: "flex", justifyContent: "space-between" }}>
      <span>Mínimo {JUSTIFICACION_MIN} caracteres. Visible para todos los revisores.</span>
      <span>
        {cantidad} / {JUSTIFICACION_MAX}
      </span>
    </span>
  );

  return (
    <section className="adoc-form-section" id="justif">
      <header>
        <h3>4 · Justificación</h3>
        <div className="hint">
          ¿Por qué este pedido? Lo lee el Coordinador, la Secretaría y el Decanato.
        </div>
      </header>
      <div className="body">
        <div className="col-12">
          <Field label="Motivo del pedido" required wide hint={hint}>
            <Textarea
              rows={4}
              maxLength={JUSTIFICACION_MAX}
              value={justificacion}
              onChange={(e) => onCambiar(e.target.value)}
            />
          </Field>
        </div>
      </div>
    </section>
  );
}
