import { ComboboxBuscable } from "../../../shared/ui/ComboboxBuscable";
import { PERSONAS_CANDIDATAS } from "../api/personasSeed";

interface SelectorResponsableProps {
  /** Nombre de la persona seleccionada, o "" si no hay selección. */
  valor: string;
  onChange: (nombre: string) => void;
  ariaLabel?: string;
  invalid?: boolean;
}

/**
 * Combobox buscable para elegir un Responsable: se tipea texto y se
 * selecciona de la lista de candidatos (`api/personasSeed.ts`). Mismo
 * componente en el filtro Responsable del listado y en el campo
 * Responsable del formulario "Nueva Tarea" — es la misma pregunta
 * ("elegí una persona buscando por texto") en los dos lugares.
 */
export function SelectorResponsable({
  valor,
  onChange,
  ariaLabel = "Responsable",
  invalid,
}: SelectorResponsableProps) {
  return (
    <ComboboxBuscable
      valorSeleccionado={valor}
      opciones={PERSONAS_CANDIDATAS.map((p) => ({
        value: p.nombre,
        label: `${p.nombre} — ${p.rol}`,
      }))}
      placeholder="Buscar persona…"
      ariaLabel={ariaLabel}
      onSeleccionar={onChange}
      invalid={invalid}
    />
  );
}
