import { useState } from "react";
import { Button, Input } from "@ars-docendi/ui";
import { PopupBuscarResponsable } from "./PopupBuscarResponsable";
import type { PersonaCandidata, PersonaTarea } from "../types";
import "./selectorResponsable.css";

interface SelectorResponsableProps {
  /** Persona elegida, o `null` si todavía no hay selección. */
  valor: PersonaTarea | null;
  onChange: (persona: PersonaTarea) => void;
  /** Acota los candidatos a quienes pueden ser Responsable de un Proyecto. */
  paraProyecto?: boolean;
  ariaLabel?: string;
  invalid?: boolean;
}

/**
 * Campo de Responsable: muestra a la persona elegida y abre un buscador emergente
 * (`PopupBuscarResponsable`) para elegir o cambiarla. Mismo componente en "Nueva Tarea" y
 * "Nuevo Proyecto"; los candidatos válidos los define el servidor según la jerarquía.
 */
export function SelectorResponsable({
  valor,
  onChange,
  paraProyecto = false,
  ariaLabel = "Responsable",
  invalid,
}: SelectorResponsableProps) {
  const [buscando, setBuscando] = useState(false);

  function elegir(persona: PersonaCandidata) {
    onChange({ id: persona.id, nombre: persona.nombre, rol: persona.rol });
  }

  return (
    <>
      <div className="adoc-selector-responsable">
        <Input
          readOnly
          value={valor ? `${valor.nombre} — ${valor.rol}` : ""}
          placeholder="Ningún responsable seleccionado"
          aria-label={ariaLabel}
          invalid={invalid}
          onClick={() => setBuscando(true)}
        />
        <Button variant="secondary" onClick={() => setBuscando(true)}>
          {valor ? "Cambiar" : "Buscar"}
        </Button>
      </div>
      <PopupBuscarResponsable
        open={buscando}
        paraProyecto={paraProyecto}
        onSeleccionar={elegir}
        onCerrar={() => setBuscando(false)}
      />
    </>
  );
}
