import { useState } from "react";
import { Input } from "@ars-docendi/ui";
import "./ComboboxBuscable.css";

export interface OpcionCombobox {
  value: string;
  label: string;
}

interface ComboboxBuscableProps {
  valorSeleccionado: string;
  opciones: OpcionCombobox[];
  placeholder?: string;
  ariaLabel: string;
  onSeleccionar: (valor: string) => void;
  invalid?: boolean;
}

/**
 * Combobox con búsqueda por texto: se tipea y la lista se filtra, se elige
 * un resultado de la lista desplegada — en vez de un `<select>` nativo o un
 * campo de texto libre. Usado por `FiltrosLista` (tipo `"buscable"`) y por
 * cualquier formulario que necesite elegir una opción de una lista larga
 * (ej. `features/tareas/components/SelectorResponsable.tsx`).
 */
export function ComboboxBuscable({
  valorSeleccionado,
  opciones,
  placeholder,
  ariaLabel,
  onSeleccionar,
  invalid,
}: ComboboxBuscableProps) {
  const opcionActual = opciones.find((o) => o.value === valorSeleccionado);
  const [query, setQuery] = useState(opcionActual?.label ?? "");
  const [abierto, setAbierto] = useState(false);

  // Ajusta el texto visible cuando el valor seleccionado cambia por fuera
  // (ej. se limpia el filtro): patrón "ajustar estado durante el render" de
  // React, no un efecto — evita el render en cascada de `useEffect`.
  const [valorPrevio, setValorPrevio] = useState(valorSeleccionado);
  if (valorSeleccionado !== valorPrevio) {
    setValorPrevio(valorSeleccionado);
    setQuery(opcionActual?.label ?? "");
  }

  const normalizado = query.trim().toLowerCase();
  const filtradas = normalizado
    ? opciones.filter((o) => o.label.toLowerCase().includes(normalizado))
    : opciones;

  return (
    <span className="adoc-combobox">
      <Input
        value={query}
        placeholder={placeholder}
        aria-label={ariaLabel}
        role="combobox"
        aria-expanded={abierto}
        autoComplete="off"
        invalid={invalid}
        onChange={(e) => {
          setQuery(e.target.value);
          setAbierto(true);
          if (valorSeleccionado) onSeleccionar("");
        }}
        onFocus={() => setAbierto(true)}
        onBlur={() => setTimeout(() => setAbierto(false), 120)}
      />
      {abierto && filtradas.length > 0 && (
        <ul className="adoc-combobox-lista" role="listbox">
          {filtradas.map((op) => (
            <li key={op.value}>
              <button
                type="button"
                role="option"
                aria-selected={op.value === valorSeleccionado}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => {
                  onSeleccionar(op.value);
                  setQuery(op.label);
                  setAbierto(false);
                }}
              >
                {op.label}
              </button>
            </li>
          ))}
        </ul>
      )}
    </span>
  );
}
