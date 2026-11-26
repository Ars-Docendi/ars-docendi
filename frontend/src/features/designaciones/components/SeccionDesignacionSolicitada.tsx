import { Field, Input, Select, Toggle } from "@ars-docendi/ui";
import type { Cargo, Dedicacion, SectorExterno } from "../types";
import type { ErroresValidacion } from "../pedidoValidacion";
import { SeccionMateriaHoras } from "./SeccionMateriaHoras";

interface SeccionDesignacionSolicitadaProps {
  /** La cátedra del pedido. No se elige: viene del ámbito del actor. */
  materia: string;
  horas: number;
  cargoSolicitado?: Cargo;
  dedicacionSolicitada?: Dedicacion;
  horasInvestigacion: number;
  horasExternas: number;
  errores: ErroresValidacion;
  onCambiarHoras: (horas: number) => void;
  onCargo: (valor?: Cargo) => void;
  onDedicacion: (valor?: Dedicacion) => void;
  onHorasInvestigacion: (valor: number) => void;
  onHorasExternas: (valor: number) => void;
  cargos: Cargo[];
  dedicaciones: Dedicacion[];
  /** `undefined` = docente interno; `""` = externo todavía sin sector elegido. */
  sectorExternoId?: string;
  sectoresExternos: SectorExterno[];
  onExterno: (externo: boolean) => void;
  onSectorExterno: (id: string) => void;
}

/**
 * Sección "Designación solicitada" (Alta / Cambio): cargo (selección libre
 * entre todo el catálogo, sin restricción de jerarquía — ver D-6) +
 * dedicación de elección libre, la materia contextual ya elegida con su carga
 * horaria, y horas de investigación/externas. Un docente externo (lo paga otro
 * departamento o área) no lleva cargo ni dedicación: se elige su sector.
 */
export function SeccionDesignacionSolicitada({
  materia,
  horas,
  cargoSolicitado,
  dedicacionSolicitada,
  horasInvestigacion,
  horasExternas,
  errores,
  onCambiarHoras,
  onCargo,
  onDedicacion,
  onHorasInvestigacion,
  onHorasExternas,
  cargos,
  dedicaciones,
  sectorExternoId,
  sectoresExternos,
  onExterno,
  onSectorExterno,
}: SeccionDesignacionSolicitadaProps) {
  const externo = sectorExternoId !== undefined;
  return (
    <section className="adoc-pf-sec">
      <h2 className="adoc-pf-sec-h">Designación solicitada</h2>
      <Field>
        <Toggle
          label="Docente externo"
          checked={externo}
          onChange={(e) => onExterno(e.target.checked)}
        />
      </Field>
      {externo && (
        <div className="adoc-pf-row">
          <Field label="Sector externo" error={errores.sectorExterno}>
            <Select value={sectorExternoId ?? ""} onChange={(e) => onSectorExterno(e.target.value)}>
              <option value="">Seleccioná el sector que lo designa…</option>
              {sectoresExternos.map((sector) => (
                <option key={sector.id} value={sector.id}>
                  {sector.nombre} ({sector.codigo})
                </option>
              ))}
            </Select>
          </Field>
        </div>
      )}
      <div className="adoc-pf-row">
        <Field label="Cargo solicitado" error={errores.cargoSolicitado}>
          <Select
            disabled={externo}
            value={cargoSolicitado ?? ""}
            onChange={(e) => onCargo((e.target.value || undefined) as Cargo)}
          >
            <option value="">Seleccioná un cargo…</option>
            {cargos.map((cargo) => (
              <option key={cargo} value={cargo}>
                {cargo}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Dedicación solicitada" error={errores.dedicacionSolicitada}>
          <Select
            disabled={externo}
            value={dedicacionSolicitada ?? ""}
            onChange={(e) => onDedicacion((e.target.value || undefined) as Dedicacion)}
          >
            <option value="">Seleccioná una dedicación…</option>
            {dedicaciones.map((dedicacion) => (
              <option key={dedicacion} value={dedicacion}>
                {dedicacion}
              </option>
            ))}
          </Select>
        </Field>
      </div>

      <SeccionMateriaHoras
        materia={materia}
        etiquetaMateria="Materia seleccionada"
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
