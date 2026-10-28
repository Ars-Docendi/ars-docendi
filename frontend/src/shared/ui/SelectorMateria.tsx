import { useState } from "react";
import { Button, Input } from "@ars-docendi/ui";
import { BuscadorMaterias } from "./BuscadorMaterias";
import {
  describirSeleccion,
  type ModoBuscadorMaterias,
  type OpcionMateriaCarrera,
  type ResultadoMateria,
  type SeleccionMateria,
} from "./busquedaMaterias";
import "./SelectorMateria.css";

interface SelectorMateriaProps {
  modo: ModoBuscadorMaterias;
  opciones: OpcionMateriaCarrera[];
  seleccion: SeleccionMateria | null;
  onChange: (resultado: ResultadoMateria) => void;
  contexto?: string;
  motivoNoElegible?: (resultado: ResultadoMateria) => string | undefined;
  ariaLabel: string;
  placeholder?: string;
  invalid?: boolean;
  /** Muestra la selección sin poder cambiarla. */
  soloLectura?: boolean;
}

/**
 * Campo de materia: muestra la selección y abre el buscador emergente (`BuscadorMaterias`)
 * para elegirla o cambiarla. Equivalente al selector de Responsable de Tareas.
 */
export function SelectorMateria({
  modo,
  opciones,
  seleccion,
  onChange,
  contexto,
  motivoNoElegible,
  ariaLabel,
  placeholder = "Ninguna materia seleccionada",
  invalid,
  soloLectura = false,
}: SelectorMateriaProps) {
  const [buscando, setBuscando] = useState(false);
  const texto = describirSeleccion(opciones, seleccion, modo);

  return (
    <>
      <div className="adoc-selector-materia">
        <Input
          readOnly
          disabled={soloLectura}
          value={texto}
          placeholder={placeholder}
          aria-label={ariaLabel}
          invalid={invalid}
          // Ancho preferido según el texto: un modal que se ajusta al contenido deja leer
          // materia y carrera completas.
          size={Math.max(texto.length, placeholder.length) + 2}
          onClick={soloLectura ? undefined : () => setBuscando(true)}
        />
        {!soloLectura && (
          <Button type="button" variant="secondary" onClick={() => setBuscando(true)}>
            {texto ? "Cambiar" : "Buscar"}
          </Button>
        )}
      </div>
      <BuscadorMaterias
        open={buscando}
        modo={modo}
        opciones={opciones}
        seleccion={seleccion}
        contexto={contexto}
        motivoNoElegible={motivoNoElegible}
        onSeleccionar={onChange}
        onCerrar={() => setBuscando(false)}
      />
    </>
  );
}
