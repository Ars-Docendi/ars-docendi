import { Field, Select } from "@ars-docendi/ui";
import type { Cargo, Dedicacion } from "../types";
import type { ErroresValidacion } from "../pedidoValidacion";
import { CARGOS, DEDICACIONES, MATERIAS } from "../api/catalogos";

interface SeccionDesignacionSolicitadaProps {
  /** En Alta se elige la materia; en Cambio la materia es la vigente del docente. */
  esAlta: boolean;
  materia: string;
  cargoSolicitado?: Cargo;
  dedicacionSolicitada?: Dedicacion;
  errores: ErroresValidacion;
  onMateria: (valor: string) => void;
  onCargo: (valor?: Cargo) => void;
  onDedicacion: (valor?: Dedicacion) => void;
}

/** Sección "Designación solicitada" (Alta / Cambio): materia + cargo + dedicación. */
export function SeccionDesignacionSolicitada({
  esAlta,
  materia,
  cargoSolicitado,
  dedicacionSolicitada,
  errores,
  onMateria,
  onCargo,
  onDedicacion,
}: SeccionDesignacionSolicitadaProps) {
  return (
    <section className="adoc-pf-sec">
      <h2 className="adoc-pf-sec-h">Designación solicitada</h2>
      {esAlta && (
        <Field label="Materia asociada" error={errores.materiaAsociada}>
          <Select value={materia} onChange={(e) => onMateria(e.target.value)}>
            <option value="">Seleccioná una materia…</option>
            {MATERIAS.map((opcion) => (
              <option key={opcion} value={opcion}>
                {opcion}
              </option>
            ))}
          </Select>
        </Field>
      )}
      <div className="adoc-pf-row">
        <Field label="Cargo solicitado" error={errores.cargoSolicitado}>
          <Select
            value={cargoSolicitado ?? ""}
            onChange={(e) => onCargo((e.target.value || undefined) as Cargo)}
          >
            <option value="">Seleccioná un cargo…</option>
            {CARGOS.map((cargo) => (
              <option key={cargo} value={cargo}>
                {cargo}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Dedicación solicitada" error={errores.dedicacionSolicitada}>
          <Select
            value={dedicacionSolicitada ?? ""}
            onChange={(e) => onDedicacion((e.target.value || undefined) as Dedicacion)}
          >
            <option value="">Seleccioná una dedicación…</option>
            {DEDICACIONES.map((dedicacion) => (
              <option key={dedicacion} value={dedicacion}>
                {dedicacion}
              </option>
            ))}
          </Select>
        </Field>
      </div>
    </section>
  );
}
