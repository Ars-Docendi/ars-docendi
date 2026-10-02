import { Field, Input } from "@ars-docendi/ui";

interface SeccionMateriaHorasProps {
  /** Materia ya elegida en el contexto del docente. Siempre de solo lectura aquí. */
  materia: string;
  etiquetaMateria?: string;
  horas: number;
  /** Alta y Cambio permiten editar la carga horaria; Baja y Sin novedad no. */
  horasEditables?: boolean;
  error?: string;
  onCambiarHoras?: (horas: number) => void;
}

/**
 * Materia y carga horaria del pedido.
 *
 * Un pedido cubre EXACTAMENTE UNA materia. La elección contextual vive en la
 * sección del docente; acá sólo se muestra y se edita la carga horaria cuando
 * corresponde (Alta o Cambio).
 *
 * Reemplaza al listado 1..N anterior, que permitía elegir materias de otras
 * carreras y dejaba a dos Coordinadores compitiendo por el mismo pedido.
 */
export function SeccionMateriaHoras({
  materia,
  etiquetaMateria = "Materia",
  horas,
  horasEditables = false,
  error,
  onCambiarHoras,
}: SeccionMateriaHorasProps) {
  return (
    <div className="adoc-pf-materias">
      <span className="adoc-pf-materias-h">Materia y horas</span>
      <div className="adoc-pf-materias-fila">
        <Field label={etiquetaMateria}>
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
