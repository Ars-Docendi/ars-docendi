import { Field, Input, Select } from "@ars-docendi/ui";
import {
  CARGOS,
  DEDICACIONES,
  MATERIAS,
  exigeDocumentacion,
  type DesignacionSolicitada,
  type TipoPedido,
} from "../mock/mockPedido";

interface SeccionDesignacionProps {
  tipo: TipoPedido;
  designacion: DesignacionSolicitada;
  onCambiar: <K extends keyof DesignacionSolicitada>(
    campo: K,
    valor: DesignacionSolicitada[K],
  ) => void;
}

/** Sección 3 — materia, cargo, horas, dedicación y antigüedad solicitadas. */
export function SeccionDesignacion({ tipo, designacion, onCambiar }: SeccionDesignacionProps) {
  const altaNueva = exigeDocumentacion(tipo);

  return (
    <section className="adoc-form-section" id="designacion">
      <header>
        <h3>3 · Designación solicitada</h3>
        <div className="hint">
          Materia, cargo, horas y antigüedad. Si es Cambio de cargo, completá el estado actual y el
          solicitado.
        </div>
      </header>
      <div className="body">
        <div className="col-6">
          <Field label="Materia" required hint="Lun/Mie 18–22h · Aula 304">
            <Select
              value={designacion.materia}
              onChange={(e) => onCambiar("materia", e.target.value)}
            >
              {MATERIAS.map((m) => (
                <option key={m} value={m}>
                  {m}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="col-6">
          <Field label="Comisión">
            <Select
              value={designacion.comision}
              onChange={(e) => onCambiar("comision", e.target.value)}
            >
              <option>02 · Cát. Ruiz · Noche</option>
            </Select>
          </Field>
        </div>

        <div className="col-4">
          <Field label="Cargo" required>
            <Select value={designacion.cargo} onChange={(e) => onCambiar("cargo", e.target.value)}>
              {CARGOS.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="col-2">
          <Field label="Horas" required>
            <Input
              type="number"
              value={designacion.horas}
              onChange={(e) => onCambiar("horas", e.target.value)}
            />
          </Field>
        </div>
        <div className="col-3">
          <Field label="Dedicación">
            <Select
              value={designacion.dedicacion}
              onChange={(e) => onCambiar("dedicacion", e.target.value)}
            >
              {DEDICACIONES.map((d) => (
                <option key={d} value={d}>
                  {d}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="col-3">
          <Field label="Antigüedad">
            <Input
              value={designacion.antiguedad}
              onChange={(e) => onCambiar("antiguedad", e.target.value)}
              disabled={altaNueva}
            />
          </Field>
        </div>

        {tipo === "cambio" && (
          <div className="col-12">
            <div className="pedido-cambio-box">
              <div className="titulo">Cambio solicitado</div>
              <div className="fila">
                <div>
                  <b>Actual:</b> Auxiliar de 1ª · 6 hs · Simple
                </div>
                <span className="sep">→</span>
                <div>
                  <b>Solicitado:</b> {designacion.cargo} · {designacion.horas} hs ·{" "}
                  {designacion.dedicacion}
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </section>
  );
}
