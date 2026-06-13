import { Field, Input } from "@ars-docendi/ui";
import { exigeDocumentacion, type DatosDocente, type TipoPedido } from "../mock/mockPedido";

interface SeccionDocenteProps {
  tipo: TipoPedido;
  docente: DatosDocente;
  onCambiar: <K extends keyof DatosDocente>(campo: K, valor: DatosDocente[K]) => void;
}

/** Sección 2 — datos de identidad del docente. */
export function SeccionDocente({ tipo, docente, onCambiar }: SeccionDocenteProps) {
  // En "Alta nueva" el docente aún no tiene legajo ni email institucional: se asignan al aprobar.
  const altaNueva = exigeDocumentacion(tipo);

  return (
    <section className="adoc-form-section" id="docente">
      <header>
        <h3>2 · Datos del docente</h3>
        <div className="hint">
          Identidad. Si el docente ya está en el sistema, completar el DNI auto-rellena los demás
          campos.
        </div>
      </header>
      <div className="body">
        <div className="col-4">
          <Field label="DNI" required hint="7 a 9 dígitos.">
            <Input
              value={docente.documento}
              onChange={(e) => onCambiar("documento", e.target.value)}
            />
          </Field>
        </div>
        <div className="col-5">
          <Field label="Nombre y apellido" required>
            <Input
              value={docente.nombreApellido}
              onChange={(e) => onCambiar("nombreApellido", e.target.value)}
            />
          </Field>
        </div>
        <div className="col-3">
          <Field label="Legajo (si existe)">
            <Input
              value={docente.legajo}
              onChange={(e) => onCambiar("legajo", e.target.value)}
              disabled={altaNueva}
              placeholder={altaNueva ? "Se asigna al aprobar" : ""}
            />
          </Field>
        </div>

        <div className="col-6">
          <Field label="Email institucional">
            <Input
              type="email"
              value={docente.emailInstitucional}
              onChange={(e) => onCambiar("emailInstitucional", e.target.value)}
              disabled={altaNueva}
              placeholder={altaNueva ? "Se asigna al aprobar" : ""}
            />
          </Field>
        </div>
        <div className="col-6">
          <Field label="Teléfono de contacto">
            <Input
              value={docente.telefono}
              onChange={(e) => onCambiar("telefono", e.target.value)}
            />
          </Field>
        </div>
      </div>
    </section>
  );
}
