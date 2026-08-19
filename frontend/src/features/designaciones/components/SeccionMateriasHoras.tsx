import { Button, Field, Input, Select } from "@ars-docendi/ui";
import type { AsignacionMateria } from "../types";
import { MATERIAS } from "../api/catalogos";
import { IconoPlus, IconoTrash2 } from "./lucide";

interface SeccionMateriasHorasProps {
  asignaciones: AsignacionMateria[];
  /** Baja: mismo listado del docente, pero sin ningún control editable. */
  soloLectura?: boolean;
  error?: string;
  onAgregar?: () => void;
  onQuitar?: (indice: number) => void;
  onCambiarMateria?: (indice: number, materia: string) => void;
  onCambiarHoras?: (indice: number, horas: number) => void;
}

/**
 * Lista de materias + horas del pedido. En Alta y Cambio es totalmente
 * editable (agregar/quitar/seleccionar materia, editar horas) y puede quedar
 * sin ninguna fila — ninguna de las dos novedades exige un mínimo de
 * materias (regla de negocio). En Baja se usa en modo `soloLectura`: mismos
 * datos, sin ningún control interactivo.
 */
export function SeccionMateriasHoras({
  asignaciones,
  soloLectura = false,
  error,
  onAgregar,
  onQuitar,
  onCambiarMateria,
  onCambiarHoras,
}: SeccionMateriasHorasProps) {
  return (
    <div className="adoc-pf-materias">
      <span className="adoc-pf-materias-h">
        {soloLectura ? "Materias del docente" : "Materias y horas asignadas"}
      </span>
      {asignaciones.map((asignacion, indice) => (
        <div className="adoc-pf-materias-fila" key={indice}>
          <Field label="Materia">
            {soloLectura ? (
              <div className="adoc-pf-materias-ro">{asignacion.materia}</div>
            ) : (
              <Select
                value={asignacion.materia}
                onChange={(e) => onCambiarMateria?.(indice, e.target.value)}
              >
                <option value="">Seleccioná una materia…</option>
                {MATERIAS.map((opcion) => (
                  <option key={opcion} value={opcion}>
                    {opcion}
                  </option>
                ))}
              </Select>
            )}
          </Field>
          <div className="adoc-pf-materias-horas">
            <Field label="Horas">
              {soloLectura ? (
                <div className="adoc-pf-materias-ro">{asignacion.horas}</div>
              ) : (
                <Input
                  type="number"
                  min={0}
                  value={asignacion.horas}
                  onChange={(e) => onCambiarHoras?.(indice, Number(e.target.value))}
                />
              )}
            </Field>
          </div>
          {!soloLectura && (
            <button
              type="button"
              className="adoc-pf-materias-quitar"
              onClick={() => onQuitar?.(indice)}
              aria-label={`Quitar materia ${asignacion.materia || indice + 1}`}
            >
              <IconoTrash2 />
            </button>
          )}
        </div>
      ))}
      {error && <p className="adoc-pf-materias-error">{error}</p>}
      {!soloLectura && (
        <Button type="button" variant="ghost" leadingIcon={<IconoPlus />} onClick={onAgregar}>
          Agregar materia
        </Button>
      )}
    </div>
  );
}
