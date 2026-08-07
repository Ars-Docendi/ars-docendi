import { Field, Input, Select } from "@ars-docendi/ui";
import type { Cargo, Dedicacion } from "../types";
import type { ErroresValidacion } from "../pedidoValidacion";
import { CARGOS, DEDICACIONES, indiceDedicacion } from "../api/catalogos";
import { SeccionMateriaHoras } from "./SeccionMateriaHoras";

interface SeccionDesignacionSolicitadaProps {
  /** La cátedra del pedido. No se elige: viene del ámbito del actor. */
  materia: string;
  horas: number;
  cargoSolicitado?: Cargo;
  dedicacionSolicitada?: Dedicacion;
  /** Cambio: dedicación vigente del docente. Filtra el Select a solo mejores (Categoría 0 = máxima). En Alta es `null` (no hay restricción). */
  dedicacionActual: Dedicacion | null;
  horasInvestigacion: number;
  horasExternas: number;
  errores: ErroresValidacion;
  onCambiarHoras: (horas: number) => void;
  onCargo: (valor?: Cargo) => void;
  onDedicacion: (valor?: Dedicacion) => void;
  onHorasInvestigacion: (valor: number) => void;
  onHorasExternas: (valor: number) => void;
}

/**
 * Sección "Designación solicitada" (Alta / Cambio): cargo (selección libre
 * entre todo el catálogo, sin restricción de jerarquía — ver D-6) +
 * dedicación (en Cambio, solo opciones mejores que la actual — ver D-7), la
 * materia de la cátedra con su carga horaria, y horas de investigación/externas.
 */
export function SeccionDesignacionSolicitada({
  materia,
  horas,
  cargoSolicitado,
  dedicacionSolicitada,
  dedicacionActual,
  horasInvestigacion,
  horasExternas,
  errores,
  onCambiarHoras,
  onCargo,
  onDedicacion,
  onHorasInvestigacion,
  onHorasExternas,
}: SeccionDesignacionSolicitadaProps) {
  const opcionesDedicacion = dedicacionActual
    ? DEDICACIONES.filter((d) => indiceDedicacion(d) < indiceDedicacion(dedicacionActual))
    : DEDICACIONES;

  return (
    <section className="adoc-pf-sec">
      <h2 className="adoc-pf-sec-h">Designación solicitada</h2>
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
            {opcionesDedicacion.map((dedicacion) => (
              <option key={dedicacion} value={dedicacion}>
                {dedicacion}
              </option>
            ))}
          </Select>
        </Field>
      </div>

      <SeccionMateriaHoras
        materia={materia}
        horas={horas}
        horasEditables
        error={errores.horas}
        onCambiarHoras={onCambiarHoras}
      />

      <div className="adoc-pf-row">
        <Field label="Horas de investigación">
          <Input
            type="number"
            min={0}
            value={horasInvestigacion}
            onChange={(e) => onHorasInvestigacion(Number(e.target.value))}
          />
        </Field>
        <Field label="Horas externas (otro depto.)">
          <Input
            type="number"
            min={0}
            value={horasExternas}
            onChange={(e) => onHorasExternas(Number(e.target.value))}
          />
        </Field>
      </div>
    </section>
  );
}
