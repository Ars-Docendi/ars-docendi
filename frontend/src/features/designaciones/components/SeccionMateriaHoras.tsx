import { Field, Input } from "@ars-docendi/ui";

interface SeccionMateriaHorasProps {
  /** La cátedra del pedido. Siempre de solo lectura: viene del ámbito del actor. */
  materia: string;
  horas: number;
  /** Alta y Cambio permiten editar la carga horaria; Baja y Sin novedad no. */
  horasEditables?: boolean;
  error?: string;
  onCambiarHoras?: (horas: number) => void;
}

/**
 * Materia y carga horaria del pedido.
 *
 * Un pedido cubre EXACTAMENTE UNA materia —la cátedra sobre la que opera el Jefe
 * de Cátedra—, así que la materia no se elige: viene del ámbito del actor y se
 * muestra de solo lectura. Lo único editable es la carga horaria, y sólo cuando la
 * novedad pide una designación (Alta o Cambio).
 *
 * Reemplaza al listado 1..N anterior, que permitía elegir materias de otras
 * carreras y dejaba a dos Coordinadores compitiendo por el mismo pedido.
 */
export function SeccionMateriaHoras({
  materia,
  horas,
  horasEditables = false,
  error,
  onCambiarHoras,
}: SeccionMateriaHorasProps) {
  return (
    <div className="adoc-pf-materias">
      <span className="adoc-pf-materias-h">Materia y horas</span>
      <div className="adoc-pf-materias-fila">
        <Field label="Materia">
          <div className="adoc-pf-materias-ro">{materia || "—"}</div>
        </Field>
        <div className="adoc-pf-materias-horas">
          <Field label="Horas" error={error}>
            {horasEditables ? (
              <Input
                type="number"
                min={0}
                value={horas}
                onChange={(e) => onCambiarHoras?.(Number(e.target.value))}
              />
            ) : (
              <div className="adoc-pf-materias-ro">{horas}</div>
            )}
          </Field>
        </div>
      </div>
    </div>
  );
}
