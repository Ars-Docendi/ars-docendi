import { ComboboxBuscable } from "../../../shared/ui/ComboboxBuscable";
import type { PersonaCandidata } from "../types";

interface SelectorResponsableProps {
  /** Nombre de la persona seleccionada, o "" si no hay selección. */
  valor: string;
  onChange: (nombre: string) => void;
  /**
   * Candidatos ofrecidos — quien llama ya filtró `personasSeed.ts` por la
   * jerarquía de asignación (`puedeAsignarComoResponsable`/
   * `puedeAsignarComoResponsableProyecto`), no el catálogo completo.
   */
  personas: PersonaCandidata[];
  ariaLabel?: string;
  invalid?: boolean;
}

/**
 * Combobox buscable para elegir un Responsable: se tipea texto y se
 * selecciona de la lista de candidatos ofrecida. Mismo componente en el
 * campo Responsable del formulario "Nueva Tarea" y "Nuevo Proyecto" — es
 * la misma pregunta ("elegí una persona buscando por texto") en los dos
 * lugares, cada uno con su propio recorte de candidatos válidos.
 */
export function SelectorResponsable({
  valor,
  onChange,
  personas,
  ariaLabel = "Responsable",
  invalid,
}: SelectorResponsableProps) {
  return (
    <ComboboxBuscable
      valorSeleccionado={valor}
      opciones={personas.map((p) => ({
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
